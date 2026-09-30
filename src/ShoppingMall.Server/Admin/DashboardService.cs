using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Admin;

/// <summary>
/// 매출 통계 조회 SQL 모음. 매출 = 주문 상품의 (주문 당시 가격 × 수량) 합계.
/// 합계는 CAST(... AS CHAR) 문자열로 내려간다. (큰 금액도 정밀도 손실 없이 전달하기 위해)
/// 취소된 주문(status = 'CANCELLED')은 매출에서 뺀다.
/// </summary>
public sealed class DashboardService
{
    /// <summary>기간 내 총 매출. 주문이 하나도 없으면 total_sales 가 null 이다.</summary>
    public Task<JsonArray> GetTotalSalesAsync(string start, string end) => QueryAsync(@"
        SELECT CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM orders o
        JOIN order_item oi ON o.order_id = oi.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'", start, end);

    /// <summary>매출액 상위 5개 상품 (상품명 기준으로 묶음)</summary>
    public Task<JsonArray> GetProductTop5Async(string start, string end) => QueryAsync(@"
        SELECT oi.product_name AS product_name,
               CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM orders o
        JOIN order_item oi ON o.order_id = oi.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'
        GROUP BY oi.product_name
        ORDER BY SUM(oi.price * oi.quantity) DESC
        LIMIT 5", start, end);

    /// <summary>카테고리별 매출 (도넛 차트). 주문 상품 → 상품 → 카테고리 순으로 JOIN 해서 묶는다.</summary>
    public Task<JsonArray> GetCategorySalesAsync(string start, string end) => QueryAsync(@"
        SELECT c.name AS name,
               CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM category c
        JOIN product p ON c.category_id = p.category_id
        JOIN order_item oi ON p.product_id = oi.product_id
        JOIN orders o ON oi.order_id = o.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'
        GROUP BY c.category_id, c.name", start, end);

    /// <summary>기간을 파라미터로 넣어 조회한다. DB 오류가 나도 예외 대신 빈 배열을 돌려준다.</summary>
    private static async Task<JsonArray> QueryAsync(string sql, string start, string end)
    {
        try
        {
            var rows = await Db.RunAsync(db => db.RowsAsync(sql, ("start", start), ("end", end)));
            return Resp.Array(rows);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[대시보드] DB 조회 실패: {e.Message}");
            return new JsonArray(); // 빈 배열이면 화면에 "데이터가 없습니다" 가 표시된다. (null 을 보내면 화면이 오류로 멈출 수 있음)
        }
    }
}
