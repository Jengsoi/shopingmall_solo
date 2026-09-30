using ShoppingMall.Protocol;

namespace ShoppingMall.Server.Data;

/// <summary>
/// 재고 변경 + 변경 이력(stock_history) 기록. 재고를 바꾸는 곳은 모두 여기를 거친다.
/// 상품을 수정하면 새 행이 생기므로, 이력은 최초 상품 ID(origin_product_id)로 묶어서 본다.
///
/// 이력 한 줄 예시:  상품 48 | 사유 ORDER_CANCEL | +3 | 15 → 18 | 주문 127 | 처리자 admin
/// 재고 변경과 이력 기록이 같은 트랜잭션 안에서 일어나므로, 둘 중 하나만 남는 일은 없다.
/// </summary>
public static class StockLedger
{
    // 이력의 reason 컬럼에 들어가는 값 (화면에서는 "주문", "주문취소" 등으로 바꿔 보여준다)
    public const string ReasonOrder = "ORDER";
    public const string ReasonOrderCancel = "ORDER_CANCEL";
    public const string ReasonProductAdd = "PRODUCT_ADD";
    public const string ReasonProductUpdate = "PRODUCT_UPDATE";
    public const string ReasonStockDecrease = "STOCK_DECREASE";

    /// <summary>
    /// 판매 중인 상품의 재고를 quantity 만큼 줄이고 이력을 남긴다. 상품이 없거나 재고가 부족하면 false.
    /// 확인과 차감을 한 문장으로 처리해서 동시 주문에서도 재고가 음수가 되지 않는다.
    /// </summary>
    public static async Task<bool> DecreaseAsync(SqlSession db, long productId, long quantity,
        string reason, long? orderId, long? memberId)
    {
        // "SELECT 로 재고 확인 → UPDATE 로 차감" 두 단계로 나누면, 그 사이에 다른 주문이 끼어들어
        // 둘 다 재고가 충분하다고 판단하고 재고가 음수가 될 수 있다.
        // WHERE stock >= @quantity 조건을 UPDATE 에 넣으면 DB 가 행을 잠근 상태에서 확인과 차감을 한 번에 한다.
        // 조건이 안 맞으면 바뀐 행이 0 개 → false.
        int updated = await db.ExecAsync(@"
            UPDATE product SET stock = stock - @quantity
            WHERE product_id = @product_id AND is_active = TRUE AND stock >= @quantity",
            ("quantity", quantity), ("product_id", productId));
        if (updated == 0)
            return false;

        await RecordAsync(db, productId, -quantity, reason, orderId, memberId);
        return true;
    }

    /// <summary>
    /// 주문 취소로 재고를 되돌린다. 주문 후 상품이 수정됐다면 지금 판매 중인 버전에 되돌린다.
    /// 되돌린 상품 ID 를 반환하고, 판매 중인 버전이 없으면 null.
    /// </summary>
    public static async Task<long?> RestoreAsync(SqlSession db, long productId, long quantity, long? orderId, long? memberId)
    {
        // src = 주문에 기록된 상품(이전 버전일 수 있음), p = 같은 최초 상품을 가진 버전 중 지금 판매 중인 것.
        // COALESCE(origin_product_id, product_id) 는 "이 행의 최초 상품 ID" (최초 상품이면 origin 이 NULL 이라 자기 ID).
        // FOR UPDATE 로 그 행을 잠가서, 복구하는 동안 다른 작업이 재고를 바꾸지 못하게 한다.
        var active = await db.OneAsync(@"
            SELECT p.product_id
            FROM product src
            JOIN product p ON COALESCE(p.origin_product_id, p.product_id) = COALESCE(src.origin_product_id, src.product_id)
            WHERE src.product_id = @product_id AND p.is_active = TRUE
            FOR UPDATE",
            ("product_id", productId));
        if (active?.Int("product_id") is not long target)
            return null;

        await db.ExecAsync("UPDATE product SET stock = stock + @quantity WHERE product_id = @product_id",
            ("quantity", quantity), ("product_id", target));
        await RecordAsync(db, target, quantity, ReasonOrderCancel, orderId, memberId);
        return target;
    }

    /// <summary>
    /// 방금 바뀐 재고를 기록한다. 같은 트랜잭션에서 이미 바뀐 상품 행의 stock 을 "변경 후" 값으로,
    /// stock - change 를 "변경 전" 값으로 남긴다.
    /// INSERT ... SELECT 한 문장으로 상품 행에서 바로 값을 읽어 넣으므로, 읽고 쓰는 사이에 값이 바뀔 틈이 없다.
    /// 예) 재고가 7 에서 20 으로 바뀐 직후 change=+13 으로 호출 → stock_before=7, stock_after=20
    /// </summary>
    public static Task RecordAsync(SqlSession db, long productId, long change, string reason, long? orderId, long? memberId) =>
        db.ExecAsync(@"
            INSERT INTO stock_history
                (product_id, origin_product_id, change_qty, stock_before, stock_after, reason, order_id, member_id)
            SELECT product_id, COALESCE(origin_product_id, product_id), @change, stock - @change, stock,
                   @reason, @order_id, @member_id
            FROM product WHERE product_id = @product_id",
            ("change", change), ("reason", reason), ("order_id", orderId), ("member_id", memberId),
            ("product_id", productId));
}
