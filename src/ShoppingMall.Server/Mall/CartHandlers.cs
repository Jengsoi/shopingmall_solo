using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>
/// 장바구니 (목록·담기·수량 변경·삭제) / 주문 (생성·목록·상세·취소).
/// 모든 기능이 로그인을 요구하고, 항상 "로그인한 본인" 의 데이터만 다룬다.
/// (WHERE 절에 member_id = 세션 회원 조건을 붙여서 남의 장바구니 행은 건드릴 수 없게 한다)
/// </summary>
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
        map["order_cancel"] = OrderCancelAsync;
    }

    /// <summary>로그인 여부 확인. member_id 는 MallServer 가 세션 값으로 넣어 준 것이다.</summary>
    private static bool TryMember(JsonObject req, out long memberId)
    {
        memberId = req.Int("member_id") ?? 0;
        return memberId != 0;
    }

    /// <summary>내 장바구니. 상품명·가격은 담을 때가 아니라 지금의 상품 정보를 보여준다(JOIN).</summary>
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

    /// <summary>장바구니 담기. 이미 담긴 상품이면 수량을 더한다.</summary>
    private static async Task<JsonObject> CartAddAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? productId = req.Int("product_id");
        if (productId is null or 0)
            return Resp.Fail("product_id가 필요합니다.");

        // quantity 키가 없으면 1개. 키는 있는데 정수가 아니면 0 으로 보고 아래에서 거부한다.
        long quantity = req["quantity"] is null ? 1 : (req.Int("quantity") ?? 0);
        if (quantity <= 0)
            return Resp.Fail("수량은 1개 이상이어야 합니다.");

        var product = await db.OneAsync(
            "SELECT stock FROM product WHERE product_id = @product_id AND is_active = TRUE",
            ("product_id", productId));
        if (product is null)
            return Resp.Fail("존재하지 않는 상품입니다.");

        // 담을 때는 안내 차원에서만 재고를 확인한다. 실제로 재고를 줄이는 것은 주문할 때다.
        long stock = product.Int("stock") ?? 0;
        if (stock < quantity)
            return Resp.Fail($"재고가 부족합니다. (남은 재고: {stock}개)");

        // cart 에 UNIQUE(member_id, product_id) 가 걸려 있어서, 이미 담긴 상품이면 INSERT 대신
        // ON DUPLICATE KEY UPDATE 부분이 실행되어 수량만 더해진다. (SELECT 후 분기하는 것보다 한 문장이라 안전)
        await db.ExecAsync(@"
            INSERT INTO cart (member_id, product_id, quantity)
            VALUES (@member_id, @product_id, @quantity)
            ON DUPLICATE KEY UPDATE quantity = quantity + VALUES(quantity)",
            ("member_id", memberId), ("product_id", productId), ("quantity", quantity));

        return Resp.Ok(message: "장바구니에 담았습니다.");
    }

    /// <summary>장바구니 수량 변경 (1 이상)</summary>
    private static async Task<JsonObject> CartUpdateAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? cartId = req.Int("cart_id");
        long? quantity = req.Int("quantity");

        if (cartId is null or 0 || quantity is null)
            return Resp.Fail("cart_id, quantity가 필요합니다.");
        if (quantity <= 0)
            return Resp.Fail("수량은 1개 이상이어야 합니다.");

        // member_id 조건 덕분에 남의 cart_id 를 보내도 아무 행도 바뀌지 않는다.
        await db.ExecAsync(
            "UPDATE cart SET quantity = @quantity WHERE cart_id = @cart_id AND member_id = @member_id",
            ("quantity", quantity), ("cart_id", cartId), ("member_id", memberId));
        return Resp.Ok(message: "수량이 변경되었습니다.");
    }

    /// <summary>장바구니에서 한 항목 삭제</summary>
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
    /// 주문 생성.
    /// order_items: [{"cart_id": 3, "product_id": 7, "quantity": 2}, ...]
    /// cart_id 가 있으면 주문 후 해당 장바구니 행도 삭제한다.
    /// 전체가 한 트랜잭션이라 상품 하나라도 재고가 부족하면 BusinessException → 주문·재고 차감·장바구니 삭제가 모두 롤백된다.
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

        // 2) 주문서(orders) 생성. 상태는 결제완료로 시작한다.
        await db.ExecAsync("INSERT INTO orders (member_id, status) VALUES (@member_id, @status)",
            ("member_id", memberId), ("status", OrderStatus.Paid));
        long orderId = db.LastInsertId;

        // 3) 상품마다: 재고 확인 → 재고 차감(+이력) → 주문 상품(order_item) 저장 → 장바구니에서 제거
        foreach (var (productId, quantity, cartId) in parsed)
        {
            // 최신 상품명/가격/재고를 서버에서 직접 조회 (클라이언트가 보낸 가격 등은 믿지 않음)
            var product = await db.OneAsync(
                "SELECT name, price, stock FROM product WHERE product_id = @product_id AND is_active = TRUE",
                ("product_id", productId));

            if (product is null)
                throw new BusinessException($"상품(product_id={productId})을 찾을 수 없습니다.");

            // 남은 재고를 메시지에 보여 주기 위한 1차 확인. 동시 주문에 대한 진짜 보호는 아래 DecreaseAsync 가 한다.
            string name = product.Str("name") ?? "";
            long stock = product.Int("stock") ?? 0;
            if (stock < quantity)
                throw new BusinessException($"{name} 재고가 부족합니다. (남은 재고: {stock}개)");

            // 재고 차감(+이력)은 order_item INSERT 보다 먼저 해야 한다: INSERT 의 외래키 검사가 상품 행에
            // 공유 잠금을 걸고 나서 UPDATE 가 배타 잠금을 요청하면, 같은 상품을 동시에 주문하는 트랜잭션끼리
            // 교착(deadlock)에 빠진다.
            if (!await StockLedger.DecreaseAsync(db, productId, quantity, StockLedger.ReasonOrder, orderId, memberId))
                throw new BusinessException($"{name} 재고가 부족합니다.");

            // 주문 시점의 상품명·가격을 복사해 둔다. 나중에 상품 정보가 바뀌어도 주문 내역은 그대로 남는다.
            await db.ExecAsync(@"
                INSERT INTO order_item (order_id, product_id, product_name, price, quantity)
                VALUES (@order_id, @product_id, @product_name, @price, @quantity)",
                ("order_id", orderId), ("product_id", productId), ("product_name", name),
                ("price", product.Int("price")), ("quantity", quantity));

            if (cartId is not null && cartId != 0)
            {
                await db.ExecAsync("DELETE FROM cart WHERE cart_id = @cart_id AND member_id = @member_id",
                    ("cart_id", cartId), ("member_id", memberId));
            }
        }

        return Resp.Ok(new JsonObject { ["order_id"] = orderId });
    }

    /// <summary>
    /// 내 주문 목록 (최신순). 주문마다 결제금액(가격 × 수량의 합)을 함께 계산한다.
    /// LEFT JOIN 이라 상품이 없는 주문도 목록에서 빠지지 않고, 이때 합계는 COALESCE 로 0 이 된다.
    /// </summary>
    private static async Task<JsonObject> OrderListAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        var rows = await db.RowsAsync(@"
            SELECT o.order_id, o.status, o.ordered_at,
                   CAST(COALESCE(SUM(oi.price * oi.quantity), 0) AS SIGNED) AS total_price
            FROM orders o
            LEFT JOIN order_item oi ON o.order_id = oi.order_id
            WHERE o.member_id = @member_id
            GROUP BY o.order_id, o.status, o.ordered_at
            ORDER BY o.order_id DESC",
            ("member_id", memberId));
        return Resp.Ok(Resp.Array(rows));
    }

    /// <summary>
    /// 주문을 취소하고 주문 수량만큼 재고를 되돌린다. 고객은 본인의 결제완료 주문만 취소할 수 있다.
    /// (상태 규칙은 OrderWorkflow 참고)
    /// </summary>
    private static async Task<JsonObject> OrderCancelAsync(SqlSession db, JsonObject req, Session s)
    {
        if (!TryMember(req, out var memberId)) return Resp.Fail("로그인이 필요합니다.");

        long? orderId = req.Int("order_id");
        if (orderId is null or 0)
            return Resp.Fail("order_id가 필요합니다.");

        // 취소할 수 없는 상태면 OrderWorkflow 가 BusinessException 을 던지고, MallServer 가 실패 응답으로 바꾼다.
        string message = await OrderWorkflow.CancelAsync(db, orderId.Value, memberId, s.IsAdmin);
        return Resp.Ok(new JsonObject { ["order_id"] = orderId }, message);
    }

    /// <summary>주문 상세 (주문 정보 + 주문 상품 목록 items)</summary>
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
        // 존재하지 않는 주문과 남의 주문을 같은 메시지로 답해서, 다른 사람의 주문번호가 있는지도 알 수 없게 한다.
        if (order is null || (order.Int("member_id") != memberId && !s.IsAdmin))
            return Resp.Fail("주문을 찾을 수 없습니다.");

        order["items"] = await OrderWorkflow.ItemsAsync(db, orderId.Value);
        return Resp.Ok(order);
    }
}
