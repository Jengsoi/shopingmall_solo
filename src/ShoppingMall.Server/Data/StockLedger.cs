using ShoppingMall.Protocol;

namespace ShoppingMall.Server.Data;

/// <summary>
/// 재고 변경 + 변경 이력(stock_history) 기록. 재고를 바꾸는 곳은 모두 여기를 거친다.
/// 상품을 수정하면 새 행이 생기므로, 이력은 최초 상품 ID(origin_product_id)로 묶어서 본다.
/// </summary>
public static class StockLedger
{
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
