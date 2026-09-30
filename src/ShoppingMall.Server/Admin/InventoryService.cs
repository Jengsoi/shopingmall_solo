using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Admin;

/// <summary>
/// 재고 관리: 카테고리·상품 조회/추가/수정, 주문 시 재고 차감, 재고 변경 이력, 주문 관리 (admin/server/inventory_logic.py).
///
/// 설계 원칙
///  - 삭제 기능 없음. 상품 수정은 기존 레코드를 비활성화하고 새 레코드를 만들어 이력을 보존한다.
///  - 재고 차감은 "재고 확인 + 차감"을 UPDATE ... WHERE stock >= ? 한 문장으로 처리하고,
///    여러 상품을 한 트랜잭션에 묶어 하나라도 실패하면 전체 롤백한다.
///  - 재고가 바뀌는 모든 작업은 StockLedger 를 통해 stock_history 에 기록한다.
/// </summary>
public sealed class InventoryService
{
    /// <summary>이 수량 이하이면 "재고 부족"으로 표시한다. (0 이면 "품절")</summary>
    public const int LowStockThreshold = 5;

    public static string GetStockStatus(long stock)
    {
        if (stock == 0) return "품절";
        if (stock <= LowStockThreshold) return "재고 부족";
        return "판매 가능";
    }

    public static readonly HashSet<string> RequestTypes = new()
    {
        "category_list", "category_add", "category_update",
        "inventory_product_list", "product_add", "product_update",
        "stock_decrease", "stock_history_list",
        "admin_order_list", "admin_order_detail", "order_status_update", "admin_order_cancel",
    };

    private static JsonObject Msg(string type, bool success, string message) =>
        new() { ["type"] = type, ["success"] = success, ["message"] = message };

    /// <summary>요청 분기. 호출 전에 관리자 인증이 끝나 있어야 한다. adminId 는 이력에 남길 처리자.</summary>
    public Task<JsonObject> HandleAsync(JsonObject request, long adminId)
    {
        string type = request.Str("type") ?? "";
        Console.WriteLine($"[재고관리 요청] {type}");

        return type switch
        {
            "category_list" => GetCategoryListAsync(),
            "category_add" => AddCategoryAsync(request),
            "category_update" => UpdateCategoryAsync(request),
            "inventory_product_list" => GetProductListAsync(),
            "product_add" => AddProductAsync(request, adminId),
            "product_update" => UpdateProductAsync(request, adminId),
            "stock_decrease" => DecreaseStockAsync(request, adminId),
            "stock_history_list" => GetStockHistoryAsync(request),
            "admin_order_list" => GetOrderListAsync(request),
            "admin_order_detail" => GetOrderDetailAsync(request),
            "order_status_update" => UpdateOrderStatusAsync(request),
            "admin_order_cancel" => CancelOrderAsync(request, adminId),
            _ => Task.FromResult(new JsonObject
            {
                ["type"] = "error",
                ["success"] = false,
                ["message"] = $"지원하지 않는 재고관리 요청입니다: {type}",
            }),
        };
    }

    // ------------------------------------------------------------------
    // 카테고리
    // ------------------------------------------------------------------

    private async Task<JsonObject> GetCategoryListAsync()
    {
        try
        {
            var rows = await Db.RunAsync(db => db.RowsAsync("SELECT * FROM category ORDER BY category_id"));
            foreach (var row in rows)
                row["is_active"] = Json.IsTrue(row["is_active"]);

            var r = Msg("category_list", true, "카테고리 목록을 조회했습니다.");
            r["categories"] = Resp.Array(rows);
            return r;
        }
        catch (Exception e)
        {
            Console.WriteLine($"카테고리 조회 오류 >> {e.Message}");
            var r = Msg("category_list", false, "카테고리 목록 조회에 실패했습니다.");
            r["categories"] = new JsonArray();
            return r;
        }
    }

    private async Task<JsonObject> AddCategoryAsync(JsonObject request)
    {
        const string type = "category_add";
        string name = (request.Str("name") ?? "").Trim();
        if (name.Length == 0)
            return Msg(type, false, "카테고리명을 입력하세요.");

        try
        {
            return await Db.RunAsync(async db =>
            {
                var dup = await db.OneAsync("SELECT category_id FROM category WHERE name = @name", ("name", name));
                if (dup is not null)
                    return Msg(type, false, "이미 존재하는 카테고리명입니다.");

                await db.ExecAsync("INSERT INTO category (name, is_active) VALUES (@name, TRUE)", ("name", name));

                var r = Msg(type, true, "카테고리가 추가되었습니다.");
                r["category_id"] = db.LastInsertId;
                return r;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[카테고리 추가 오류] {e.Message}");
            return Msg(type, false, "카테고리 추가에 실패했습니다.");
        }
    }

    private async Task<JsonObject> UpdateCategoryAsync(JsonObject request)
    {
        const string type = "category_update";

        if (request["category_id"] is null)
            return Msg(type, false, "카테고리 ID가 필요합니다.");
        if (request.Int("category_id") is not long categoryId)
            return Msg(type, false, "카테고리 ID가 올바르지 않습니다.");

        string name = (request.Str("name") ?? "").Trim();
        if (name.Length == 0)
            return Msg(type, false, "카테고리명을 입력하세요.");

        if (request.Bool("is_active") is not bool isActive)
            return Msg(type, false, "활성 상태 값이 올바르지 않습니다.");

        try
        {
            return await Db.RunAsync(async db =>
            {
                var found = await db.OneAsync("SELECT category_id FROM category WHERE category_id = @id", ("id", categoryId));
                if (found is null)
                    return Msg(type, false, "존재하지 않는 카테고리입니다.");

                var dup = await db.OneAsync(
                    "SELECT category_id FROM category WHERE name = @name AND category_id != @id",
                    ("name", name), ("id", categoryId));
                if (dup is not null)
                    return Msg(type, false, "이미 존재하는 카테고리명입니다.");

                await db.ExecAsync(
                    "UPDATE category SET name = @name, is_active = @active WHERE category_id = @id",
                    ("name", name), ("active", isActive), ("id", categoryId));

                var r = Msg(type, true, "카테고리가 수정되었습니다.");
                r["category_id"] = categoryId;
                return r;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[카테고리 수정 오류] {e.Message}");
            return Msg(type, false, "카테고리 수정에 실패했습니다.");
        }
    }

    // ------------------------------------------------------------------
    // 상품
    // ------------------------------------------------------------------

    private async Task<JsonObject> GetProductListAsync()
    {
        const string type = "inventory_product_list";
        try
        {
            var rows = await Db.RunAsync(db => db.RowsAsync(@"
                SELECT
                    p.product_id, p.category_id, c.name AS category_name,
                    p.name, p.description, p.color, p.size,
                    p.price, p.stock, p.is_active, p.created_at
                FROM product p
                INNER JOIN category c ON p.category_id = c.category_id
                ORDER BY p.product_id"));

            foreach (var product in rows)
            {
                product["is_active"] = Json.IsTrue(product["is_active"]);
                product["description"] = product.Str("description") ?? "";
                product["color"] = product.Str("color") ?? "";
                product["size"] = product.Str("size") ?? "";

                long stock = product.Int("stock") ?? 0;
                // 요청 필드는 "inventory" 라서, 예전 Python 화면과도 맞도록 같은 값을 함께 내려준다.
                product["inventory"] = stock;
                product["stock_status"] = GetStockStatus(stock);
            }

            var r = Msg(type, true, "상품 목록을 조회했습니다.");
            r["products"] = Resp.Array(rows);
            return r;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[상품 조회 오류] {e.Message}");
            var r = Msg(type, false, "상품 목록 조회에 실패했습니다.");
            r["products"] = new JsonArray();
            return r;
        }
    }

    /// <summary>add/update 공통 입력 검증. 문제가 있으면 오류 메시지를 반환한다.</summary>
    private static string? ValidateProductInput(JsonObject request, bool requireProductId,
        out long productId, out long categoryId, out string name, out long price, out long stock)
    {
        productId = 0; categoryId = 0; name = ""; price = 0; stock = 0;

        if (requireProductId)
        {
            if (request.Int("product_id") is not long pid) return "수정할 상품 ID가 필요합니다.";
            productId = pid;
        }

        if (request.Int("category_id") is not long cid) return "카테고리를 선택하세요.";
        categoryId = cid;

        name = (request.Str("name") ?? "").Trim();
        if (name.Length == 0) return "상품명을 입력하세요.";

        if (request.Int("price") is not long p || p < 0) return "가격은 0 이상의 정수여야 합니다.";
        price = p;

        if (request.Int("inventory") is not long st || st < 0) return "재고수량은 0 이상의 정수여야 합니다.";
        stock = st;

        return null;
    }

    private static object? NullIfEmpty(string? s)
    {
        s = s?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private async Task<JsonObject> AddProductAsync(JsonObject request, long adminId)
    {
        const string type = "product_add";

        string? error = ValidateProductInput(request, false, out _, out long categoryId, out string name, out long price, out long stock);
        if (error is not null)
            return Msg(type, false, error);

        object? description = NullIfEmpty(request.Str("description"));
        object? color = NullIfEmpty(request.Str("color"));
        object? size = NullIfEmpty(request.Str("size"));

        try
        {
            return await Db.RunAsync(async db =>
            {
                var category = await db.OneAsync(
                    "SELECT category_id FROM category WHERE category_id = @id AND is_active = TRUE",
                    ("id", categoryId));
                if (category is null)
                    return Msg(type, false, "존재하지 않거나 비활성화된 카테고리입니다.");

                await db.ExecAsync(@"
                    INSERT INTO product (category_id, name, description, color, size, price, stock, is_active)
                    VALUES (@category_id, @name, @description, @color, @size, @price, @stock, TRUE)",
                    ("category_id", categoryId), ("name", name), ("description", description),
                    ("color", color), ("size", size), ("price", price), ("stock", stock));
                long newId = db.LastInsertId;
                await StockLedger.RecordAsync(db, newId, stock, StockLedger.ReasonProductAdd, null, adminId);

                var r = Msg(type, true, "상품이 추가되었습니다.");
                r["product_id"] = newId;
                r["stock_status"] = GetStockStatus(stock);
                return r;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[상품 추가 오류] {e.Message}");
            return Msg(type, false, "상품 추가에 실패했습니다.");
        }
    }

    /// <summary>기존 상품을 비활성화하고, 수정된 내용으로 새 상품 레코드를 만든다.</summary>
    private async Task<JsonObject> UpdateProductAsync(JsonObject request, long adminId)
    {
        const string type = "product_update";

        string? error = ValidateProductInput(request, true, out long productId, out long categoryId, out string name, out long price, out long stock);
        if (error is not null)
            return Msg(type, false, error);

        object? description = NullIfEmpty(request.Str("description"));
        object? color = NullIfEmpty(request.Str("color"));
        object? size = NullIfEmpty(request.Str("size"));

        try
        {
            return await Db.RunAsync(async db =>
            {
                // 수정 중 다른 작업이 끼어들지 못하도록 기존 상품 행을 잠근다.
                var product = await db.OneAsync(@"
                    SELECT stock, COALESCE(origin_product_id, product_id) AS origin_id
                    FROM product WHERE product_id = @id AND is_active = TRUE FOR UPDATE",
                    ("id", productId));
                if (product is null)
                    return Msg(type, false, "존재하지 않거나 비활성화된 상품입니다.");

                var category = await db.OneAsync(
                    "SELECT category_id FROM category WHERE category_id = @id AND is_active = TRUE",
                    ("id", categoryId));
                if (category is null)
                    return Msg(type, false, "존재하지 않거나 비활성화된 카테고리입니다.");

                int deactivated = await db.ExecAsync(
                    "UPDATE product SET is_active = FALSE WHERE product_id = @id AND is_active = TRUE",
                    ("id", productId));
                if (deactivated != 1)
                {
                    db.RequestRollback();
                    return Msg(type, false, "기존 상품 비활성화에 실패했습니다.");
                }

                // 새 행은 최초 상품을 가리켜서, 주문 취소 시 재고 복구와 재고 이력이 버전을 넘어 이어지게 한다.
                await db.ExecAsync(@"
                    INSERT INTO product (category_id, name, description, color, size, price, stock, is_active, origin_product_id)
                    VALUES (@category_id, @name, @description, @color, @size, @price, @stock, TRUE, @origin_id)",
                    ("category_id", categoryId), ("name", name), ("description", description),
                    ("color", color), ("size", size), ("price", price), ("stock", stock),
                    ("origin_id", product.Int("origin_id")));
                long newId = db.LastInsertId;

                // 이전 버전의 재고 → 새 버전의 재고로 바뀐 것으로 기록한다.
                long oldStock = product.Int("stock") ?? 0;
                await StockLedger.RecordAsync(db, newId, stock - oldStock, StockLedger.ReasonProductUpdate, null, adminId);

                var r = Msg(type, true, "기존 상품을 비활성화하고 새 상품을 등록했습니다.");
                r["old_product_id"] = productId;
                r["new_product_id"] = newId;
                r["stock_status"] = GetStockStatus(stock);
                return r;
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[상품 수정 오류] {e.Message}");
            return Msg(type, false, "상품 수정에 실패했습니다.");
        }
    }

    // ------------------------------------------------------------------
    // 재고 차감
    // ------------------------------------------------------------------

    /// <summary>items: [{"product_id": 1, "quantity": 2}, ...] 전체를 한 트랜잭션으로 차감한다.</summary>
    private async Task<JsonObject> DecreaseStockAsync(JsonObject request, long adminId)
    {
        const string type = "stock_decrease";

        if (request.Arr("items") is not { Count: > 0 } items)
            return Msg(type, false, "재고를 차감할 상품 목록이 없습니다.");

        try
        {
            return await Db.RunAsync(async db =>
            {
                foreach (var node in items)
                {
                    var item = node as JsonObject;
                    long? productId = item?.Int("product_id");
                    long? quantity = item?.Int("quantity");

                    if (productId is null)
                    {
                        db.RequestRollback();
                        return Msg(type, false, "상품 ID가 올바르지 않은 주문 항목이 있습니다.");
                    }
                    if (quantity is null or <= 0)
                    {
                        db.RequestRollback();
                        var bad = Msg(type, false, "주문수량은 1 이상의 정수여야 합니다.");
                        bad["product_id"] = productId;
                        return bad;
                    }

                    // 활성 상품이고 재고가 충분한 경우에만 차감 (확인과 차감이 한 문장)
                    bool decreased = await StockLedger.DecreaseAsync(db, productId.Value, quantity.Value,
                        StockLedger.ReasonStockDecrease, null, adminId);

                    if (!decreased)
                    {
                        db.RequestRollback(); // 앞 상품에서 이미 차감한 것도 되돌린다.
                        var fail = Msg(type, false, "상품이 존재하지 않거나 재고가 부족합니다.");
                        fail["product_id"] = productId;
                        return fail;
                    }
                }

                return Msg(type, true, "주문 상품의 재고가 차감되었습니다.");
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"[재고 차감 오류] {e.Message}");
            return Msg(type, false, "재고 차감에 실패했습니다.");
        }
    }

    // ------------------------------------------------------------------
    // 재고 변경 이력
    // ------------------------------------------------------------------

    /// <summary>
    /// 최근 재고 변경 이력 (최대 500건). product_id 를 주면 그 상품의 모든 버전(수정 전/후) 이력만 본다.
    /// </summary>
    private async Task<JsonObject> GetStockHistoryAsync(JsonObject request)
    {
        const string type = "stock_history_list";
        long? productId = request.Int("product_id");

        try
        {
            var rows = await Db.RunAsync(db => db.RowsAsync(@"
                SELECT h.history_id, h.created_at, h.product_id, h.origin_product_id, p.name AS product_name,
                       h.change_qty, h.stock_before, h.stock_after, h.reason, h.order_id,
                       m.login_id AS member_login_id
                FROM stock_history h
                JOIN product p ON p.product_id = h.product_id
                LEFT JOIN member m ON m.member_id = h.member_id
                WHERE @product_id IS NULL
                   OR h.origin_product_id = (SELECT COALESCE(origin_product_id, product_id)
                                             FROM product WHERE product_id = @product_id)
                ORDER BY h.history_id DESC
                LIMIT 500",
                ("product_id", productId)));

            var r = Msg(type, true, "재고 변경 이력을 조회했습니다.");
            r["history"] = Resp.Array(rows);
            return r;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[재고 이력 조회 오류] {e.Message}");
            var r = Msg(type, false, "재고 변경 이력 조회에 실패했습니다.");
            r["history"] = new JsonArray();
            return r;
        }
    }

    // ------------------------------------------------------------------
    // 주문 관리
    // ------------------------------------------------------------------

    /// <summary>전체 주문 목록 (최신순 최대 500건). status 를 주면 그 상태만.</summary>
    private async Task<JsonObject> GetOrderListAsync(JsonObject request)
    {
        const string type = "admin_order_list";
        string? status = request.Str("status");
        if (string.IsNullOrEmpty(status)) status = null;
        else if (!OrderStatus.All.Contains(status)) return Msg(type, false, "알 수 없는 주문 상태입니다.");

        try
        {
            var rows = await Db.RunAsync(db => db.RowsAsync(@"
                SELECT o.order_id, o.status, o.ordered_at, m.login_id, m.name AS member_name,
                       CAST(COALESCE(SUM(oi.price * oi.quantity), 0) AS SIGNED) AS total_price
                FROM orders o
                JOIN member m ON m.member_id = o.member_id
                LEFT JOIN order_item oi ON oi.order_id = o.order_id
                WHERE @status IS NULL OR o.status = @status
                GROUP BY o.order_id, o.status, o.ordered_at, m.login_id, m.name
                ORDER BY o.order_id DESC
                LIMIT 500",
                ("status", status)));

            var r = Msg(type, true, "주문 목록을 조회했습니다.");
            r["orders"] = Resp.Array(rows);
            return r;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[주문 목록 조회 오류] {e.Message}");
            var r = Msg(type, false, "주문 목록 조회에 실패했습니다.");
            r["orders"] = new JsonArray();
            return r;
        }
    }

    private async Task<JsonObject> GetOrderDetailAsync(JsonObject request)
    {
        const string type = "admin_order_detail";
        if (request.Int("order_id") is not long orderId)
            return Msg(type, false, "주문 ID가 필요합니다.");

        try
        {
            var items = await Db.RunAsync(db => OrderWorkflow.ItemsAsync(db, orderId));
            var r = Msg(type, true, "주문 상세를 조회했습니다.");
            r["items"] = items;
            return r;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[주문 상세 조회 오류] {e.Message}");
            return Msg(type, false, "주문 상세 조회에 실패했습니다.");
        }
    }

    /// <summary>order_id 주문을 status(다음 단계)로 진행한다.</summary>
    private Task<JsonObject> UpdateOrderStatusAsync(JsonObject request)
    {
        const string type = "order_status_update";
        if (request.Int("order_id") is not long orderId)
            return Task.FromResult(Msg(type, false, "주문 ID가 필요합니다."));
        string status = request.Str("status") ?? "";

        return RunOrderActionAsync(type, "주문 상태 변경에 실패했습니다.", async db =>
        {
            await OrderWorkflow.AdvanceAsync(db, orderId, status);
            return "주문 상태가 변경되었습니다.";
        });
    }

    private Task<JsonObject> CancelOrderAsync(JsonObject request, long adminId)
    {
        const string type = "admin_order_cancel";
        if (request.Int("order_id") is not long orderId)
            return Task.FromResult(Msg(type, false, "주문 ID가 필요합니다."));

        return RunOrderActionAsync(type, "주문 취소에 실패했습니다.",
            db => OrderWorkflow.CancelAsync(db, orderId, adminId, byAdmin: true));
    }

    /// <summary>주문 처리 공통: 규칙 위반(BusinessException)은 그 메시지를, 그 밖의 오류는 일반 메시지를 돌려준다.</summary>
    private static async Task<JsonObject> RunOrderActionAsync(string type, string failMessage, Func<SqlSession, Task<string>> action)
    {
        try
        {
            string message = await Db.RunAsync(action);
            return Msg(type, true, message);
        }
        catch (BusinessException e)
        {
            return Msg(type, false, e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{type} 오류] {e.Message}");
            return Msg(type, false, failMessage);
        }
    }
}
