using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>장바구니 / 주문 (handlers/cart_handler.py)</summary>
public static class CartHandlers
{
    public static void Register(Dictionary<string, Handler> map)
    {
        map["cart_list"] = CartListAsync;
        map["cart_add"] = CartAddAsync;
        map["cart_update"] = CartUpdateAsync;
        map["cart_delete"] = CartDeleteAsync;
        map["order_create"] = OrderCreateAsync;
        map["order_list"] = OrderListAsync;
        map["order_detail"] = OrderDetailAsync;
    }

    private static bool TryMember(JsonObject req, out long memberId)
    {
        memberId = req.Int("member_id") ?? 0;
        return memberId != 0;
    }

    private static async Task<JsonObject> CartListAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        var rows = await db.RowsAsync(@"
            SELECT cart.cart_id, cart.product_id, product.name, product.price, cart.quantity
            FROM cart
            JOIN product ON cart.product_id = product.product_id
            WHERE cart.member_id = @member_id
            ORDER BY cart.cart_id",
            ("member_id", memberId));
        return Resp.Ok(Resp.Array(rows));
    }

    private static async Task<JsonObject> CartAddAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? productId = req.Int("product_id");
        if (productId is null or 0)
            return Resp.Fail("product_id가 필요합니다.");

        // 키가 없으면 1개. (Python: request.get("quantity", 1))
        long quantity = req["quantity"] is null ? 1 : (req.Int("quantity") ?? 0);
        if (quantity <= 0)
            return Resp.Fail("수량은 1개 이상이어야 합니다.");

        var product = await db.OneAsync(
            "SELECT stock FROM product WHERE product_id = @product_id AND is_active = TRUE",
            ("product_id", productId));
        if (product is null)
            return Resp.Fail("존재하지 않는 상품입니다.");

        long stock = product.Int("stock") ?? 0;
        if (stock < quantity)
            return Resp.Fail($"재고가 부족합니다. (남은 재고: {stock}개)");

        // cart 에 UNIQUE(member_id, product_id) 가 걸려 있어서, 이미 담긴 상품이면 수량만 더한다.
        await db.ExecAsync(@"
            INSERT INTO cart (member_id, product_id, quantity)
            VALUES (@member_id, @product_id, @quantity)
            ON DUPLICATE KEY UPDATE quantity = quantity + VALUES(quantity)",
            ("member_id", memberId), ("product_id", productId), ("quantity", quantity));

        return Resp.Ok(message: "장바구니에 담았습니다.");
    }

    private static async Task<JsonObject> CartUpdateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? cartId = req.Int("cart_id");
        long? quantity = req.Int("quantity");

        if (cartId is null or 0 || quantity is null)
            return Resp.Fail("cart_id, quantity가 필요합니다.");
        if (quantity <= 0)
            return Resp.Fail("수량은 1개 이상이어야 합니다.");

        await db.ExecAsync(
            "UPDATE cart SET quantity = @quantity WHERE cart_id = @cart_id AND member_id = @member_id",
            ("quantity", quantity), ("cart_id", cartId), ("member_id", memberId));
        return Resp.Ok(message: "수량이 변경되었습니다.");
    }

    private static async Task<JsonObject> CartDeleteAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? cartId = req.Int("cart_id");
        if (cartId is null or 0)
            return Resp.Fail("cart_id가 필요합니다.");

        await db.ExecAsync(
            "DELETE FROM cart WHERE cart_id = @cart_id AND member_id = @member_id",
            ("cart_id", cartId), ("member_id", memberId));
        return Resp.Ok(message: "삭제되었습니다.");
    }

    /// <summary>
    /// order_items: [{"cart_id": 3, "product_id": 7, "quantity": 2}, ...]
    /// cart_id 가 있으면 주문 후 해당 장바구니 행도 삭제한다. 하나라도 실패하면 전체 롤백.
    /// </summary>
    private static async Task<JsonObject> OrderCreateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        if (req.Arr("order_items") is not { Count: > 0 } items)
            return Resp.Fail("주문할 상품이 없습니다.");

        // 1) 입력 검증 (음수 수량으로 재고를 늘리는 것을 막는다)
        var parsed = new List<(long ProductId, long Quantity, long? CartId)>();
        foreach (var node in items)
        {
            if (node is not JsonObject item)
                throw new BusinessException("주문 항목 형식이 올바르지 않습니다.");

            long? productId = item.Int("product_id");
            long? quantity = item.Int("quantity");

            if (productId is null)
                throw new BusinessException("상품 ID가 올바르지 않은 주문 항목이 있습니다.");
            if (quantity is null or <= 0)
                throw new BusinessException("주문수량은 1 이상의 정수여야 합니다.");

            parsed.Add((productId.Value, quantity.Value, item.Int("cart_id")));
        }

        // 2) 주문 생성
        await db.ExecAsync("INSERT INTO orders (member_id, status) VALUES (@member_id, 'PAID')", ("member_id", memberId));
        long orderId = db.LastInsertId;

        foreach (var (productId, quantity, cartId) in parsed)
        {
            // 최신 상품명/가격/재고를 서버에서 직접 조회 (클라이언트 값을 믿지 않음)
            var product = await db.OneAsync(
                "SELECT name, price, stock FROM product WHERE product_id = @product_id AND is_active = TRUE",
                ("product_id", productId));

            if (product is null)
                throw new BusinessException($"상품(product_id={productId})을 찾을 수 없습니다.");

            string name = product.Str("name") ?? "";
            long stock = product.Int("stock") ?? 0;
            if (stock < quantity)
                throw new BusinessException($"{name} 재고가 부족합니다. (남은 재고: {stock}개)");

            await db.ExecAsync(@"
                INSERT INTO order_item (order_id, product_id, product_name, price, quantity)
                VALUES (@order_id, @product_id, @product_name, @price, @quantity)",
                ("order_id", orderId), ("product_id", productId), ("product_name", name),
                ("price", product.Int("price")), ("quantity", quantity));

            // 재고 확인과 차감을 한 문장으로 처리해서 동시 주문에서도 재고가 음수가 되지 않게 한다.
            int updated = await db.ExecAsync(@"
                UPDATE product SET stock = stock - @quantity
                WHERE product_id = @product_id AND is_active = TRUE AND stock >= @quantity",
                ("quantity", quantity), ("product_id", productId));
            if (updated == 0)
                throw new BusinessException($"{name} 재고가 부족합니다.");

            if (cartId is not null && cartId != 0)
            {
                await db.ExecAsync("DELETE FROM cart WHERE cart_id = @cart_id AND member_id = @member_id",
                    ("cart_id", cartId), ("member_id", memberId));
            }
        }

        return Resp.Ok(new JsonObject { ["order_id"] = orderId });
    }

    private static async Task<JsonObject> OrderListAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        var rows = await db.RowsAsync(@"
            SELECT order_id, status, ordered_at
            FROM orders
            WHERE member_id = @member_id
            ORDER BY order_id DESC",
            ("member_id", memberId));
        return Resp.Ok(Resp.Array(rows));
    }

    private static async Task<JsonObject> OrderDetailAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? orderId = req.Int("order_id");
        if (orderId is null or 0)
            return Resp.Fail("order_id가 필요합니다.");

        var order = await db.OneAsync(
            "SELECT order_id, member_id, status, ordered_at FROM orders WHERE order_id = @order_id",
            ("order_id", orderId));

        // 남의 주문은 볼 수 없다. (관리자는 예외)
        if (order is null || (order.Int("member_id") != memberId && !s.IsAdmin))
            return Resp.Fail("주문을 찾을 수 없습니다.");

        var items = await db.RowsAsync(@"
            SELECT order_item_id, product_id, product_name, price, quantity
            FROM order_item
            WHERE order_id = @order_id",
            ("order_id", orderId));

        order["items"] = Resp.Array(items);
        return Resp.Ok(order);
    }
}
