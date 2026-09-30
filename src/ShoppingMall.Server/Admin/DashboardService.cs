using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Admin;

/// <summary>매출 통계 조회 (dashboard_server.py). 합계는 CAST(... AS CHAR) 문자열로 내려간다. 취소된 주문은 매출에서 뺀다.</summary>
public sealed class DashboardService
{
    public Task<JsonArray> GetTotalSalesAsync(string start, string end) => QueryAsync(@"
        SELECT CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM orders o
        JOIN order_item oi ON o.order_id = oi.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'", start, end);

    public Task<JsonArray> GetProductTop5Async(string start, string end) => QueryAsync(@"
        SELECT oi.product_name AS product_name,
               CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM orders o
        JOIN order_item oi ON o.order_id = oi.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'
        GROUP BY oi.product_name
        ORDER BY SUM(oi.price * oi.quantity) DESC
        LIMIT 5", start, end);

    public Task<JsonArray> GetCategorySalesAsync(string start, string end) => QueryAsync(@"
        SELECT c.name AS name,
               CAST(SUM(oi.price * oi.quantity) AS CHAR) AS total_sales
        FROM category c
        JOIN product p ON c.category_id = p.category_id
        JOIN order_item oi ON p.product_id = oi.product_id
        JOIN orders o ON oi.order_id = o.order_id
        WHERE o.ordered_at BETWEEN @start AND @end AND o.status <> 'CANCELLED'
        GROUP BY c.category_id, c.name", start, end);

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
            return new JsonArray(); // 원본은 None 을 보내 클라이언트가 죽었다. 빈 배열로 대신한다.
        }
    }
}
