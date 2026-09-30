using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Server.Data;

/// <summary>
/// 주문 상태: 결제완료 → 배송준비중 → 배송중 → 배송완료, 그리고 주문취소.
/// 상태는 관리자가 한 단계씩만 앞으로 진행할 수 있다.
///
///   PAID ──→ PREPARING ──→ SHIPPING ──→ DELIVERED
///    │           │             │
///    └───────────┴─────────────┴──→ CANCELLED   (고객은 PAID 에서만, 관리자는 셋 다)
///
/// DB 의 orders.status 컬럼에 아래 영문 값이 저장되고, 클라이언트가 한글로 바꿔 보여준다.
/// </summary>
public static class OrderStatus
{
    public const string Paid = "PAID";
    public const string Preparing = "PREPARING";
    public const string Shipping = "SHIPPING";
    public const string Delivered = "DELIVERED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All = { Paid, Preparing, Shipping, Delivered, Cancelled };

    /// <summary>다음 진행 단계. 더 진행할 수 없으면 null.</summary>
    public static string? Next(string status) => status switch
    {
        Paid => Preparing,
        Preparing => Shipping,
        Shipping => Delivered,
        _ => null,
    };

    /// <summary>고객은 결제완료 상태에서만, 관리자는 배송완료 전까지 취소할 수 있다.</summary>
    public static bool CanCancel(string status, bool byAdmin) =>
        status == Paid || (byAdmin && status is Preparing or Shipping);
}

/// <summary>주문 취소 / 상태 변경. 쇼핑몰 서버(고객)와 재고관리 서버(관리자)가 함께 쓴다.</summary>
public static class OrderWorkflow
{
    /// <summary>
    /// 주문을 취소하고 주문 수량만큼 재고를 되돌린다. 고객(byAdmin=false)은 자기 주문만 취소할 수 있다.
    /// 성공하면 안내 문구를 반환하고, 취소할 수 없으면 BusinessException.
    /// </summary>
    public static async Task<string> CancelAsync(SqlSession db, long orderId, long actorId, bool byAdmin)
    {
        // 주문 행을 잠가서 같은 주문을 동시에 취소/진행해도 재고가 두 번 복구되지 않게 한다.
        // (FOR UPDATE: 이 트랜잭션이 끝날 때까지 다른 트랜잭션은 이 행을 잠그거나 바꾸려면 기다려야 한다.
        //  두 번째 취소 요청은 첫 번째가 커밋된 뒤에 상태를 읽으므로 "이미 취소된 주문" 으로 거절된다)
        var order = await db.OneAsync(
            "SELECT member_id, status FROM orders WHERE order_id = @order_id FOR UPDATE",
            ("order_id", orderId));
        if (order is null || (!byAdmin && order.Int("member_id") != actorId))
            throw new BusinessException("주문을 찾을 수 없습니다.");

        string status = order.Str("status") ?? "";
        if (status == OrderStatus.Cancelled)
            throw new BusinessException("이미 취소된 주문입니다.");
        if (!OrderStatus.CanCancel(status, byAdmin))
            throw new BusinessException(byAdmin
                ? "배송완료된 주문은 취소할 수 없습니다."
                : "배송 준비가 시작된 주문은 취소할 수 없습니다. 고객센터에 문의하세요.");

        // 상태를 먼저 바꾸고, 주문 상품마다 재고를 되돌린다. (전체가 한 트랜잭션이라 중간에 실패하면 모두 롤백)
        await db.ExecAsync("UPDATE orders SET status = @status WHERE order_id = @order_id",
            ("status", OrderStatus.Cancelled), ("order_id", orderId));

        var items = await db.RowsAsync(
            "SELECT product_id, product_name, quantity FROM order_item WHERE order_id = @order_id",
            ("order_id", orderId));

        var notRestored = new List<string>();
        foreach (var item in items)
        {
            long? restored = await StockLedger.RestoreAsync(db,
                item.Int("product_id") ?? 0, item.Int("quantity") ?? 0, orderId, actorId);
            if (restored is null)
                notRestored.Add(item.Str("product_name") ?? "");
        }

        string message = "주문이 취소되었습니다.";
        if (notRestored.Count > 0)
            message += $"\n(판매 중인 상품이 없어 재고가 복구되지 않았습니다: {string.Join(", ", notRestored)})";
        return message;
    }

    /// <summary>
    /// 관리자가 주문을 다음 단계로 진행한다. 화면에 보이던 상태와 다르거나(다른 관리자가 먼저 바꿈)
    /// 한 단계를 건너뛰려 하면 BusinessException.
    /// </summary>
    public static async Task AdvanceAsync(SqlSession db, long orderId, string targetStatus)
    {
        var order = await db.OneAsync(
            "SELECT status FROM orders WHERE order_id = @order_id FOR UPDATE",
            ("order_id", orderId));
        if (order is null)
            throw new BusinessException("주문을 찾을 수 없습니다.");

        string status = order.Str("status") ?? "";
        if (OrderStatus.Next(status) != targetStatus)
            throw new BusinessException("현재 상태에서 바꿀 수 없는 단계입니다. 목록을 새로고침하세요.");

        await db.ExecAsync("UPDATE orders SET status = @status WHERE order_id = @order_id",
            ("status", targetStatus), ("order_id", orderId));
    }

    /// <summary>주문에 담긴 상품 목록.</summary>
    public static async Task<JsonArray> ItemsAsync(SqlSession db, long orderId) =>
        Resp.Array(await db.RowsAsync(@"
            SELECT order_item_id, product_id, product_name, price, quantity
            FROM order_item
            WHERE order_id = @order_id",
            ("order_id", orderId)));
}
