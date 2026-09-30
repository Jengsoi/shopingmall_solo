using System.Text;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>상품페이지: 카테고리 목록 / 상품 목록(검색) / 상품 상세. 고객에게는 판매 중(is_active)인 것만 보인다.</summary>
public static class ProductHandlers
{
    public static void Register(Dictionary<string, Handler> map)
    {
        map["category_list"] = CategoryListAsync;
        map["product_list"] = ProductListAsync;
        map["product_detail"] = ProductDetailAsync;
    }

    /// <summary>활성 카테고리 목록 (상품페이지의 카테고리 선택 상자)</summary>
    private static async Task<JsonObject> CategoryListAsync(SqlSession db, JsonObject req, Session s)
    {
        var rows = await db.RowsAsync(
            "SELECT category_id, name FROM category WHERE is_active = TRUE ORDER BY category_id");
        return Resp.Ok(Resp.Array(rows));
    }

    /// <summary>
    /// 상품 목록. category_id(0 이나 없음 = 전체)와 keyword(상품명 일부)로 거를 수 있다.
    /// 조건이 있을 때만 WHERE 절을 덧붙이는 방식으로 SQL 을 조립한다.
    /// </summary>
    private static async Task<JsonObject> ProductListAsync(SqlSession db, JsonObject req, Session s)
    {
        var sql = new StringBuilder(@"
            SELECT p.product_id, p.name, p.color, p.size, p.price, p.stock
            FROM product p
            WHERE p.is_active = TRUE");
        var args = new List<(string, object?)>();

        long? categoryId = req.Int("category_id");
        if (categoryId is not null && categoryId != 0)
        {
            sql.Append(" AND p.category_id = @category_id");
            args.Add(("category_id", categoryId));
        }

        string keyword = req.Str("keyword") ?? "";
        if (keyword.Length > 0)
        {
            // LIKE '%키워드%' = 상품명 어디에든 키워드가 들어 있으면. (% 는 SQL 쪽 와일드카드, 값은 여전히 파라미터로 전달)
            sql.Append(" AND p.name LIKE @keyword");
            args.Add(("keyword", $"%{keyword}%"));
        }

        sql.Append(" ORDER BY p.product_id");

        var rows = await db.RowsAsync(sql.ToString(), args.ToArray());
        return Resp.Ok(Resp.Array(rows));
    }

    /// <summary>상품 상세 (설명·카테고리명 포함). 판매가 끝난(비활성) 상품은 "찾을 수 없음".</summary>
    private static async Task<JsonObject> ProductDetailAsync(SqlSession db, JsonObject req, Session s)
    {
        long? productId = req.Int("product_id");
        if (productId is null or 0)
            return Resp.Fail("product_id가 필요합니다.");

        var row = await db.OneAsync(@"
            SELECT p.product_id, p.name, p.description, p.color, p.size,
                   p.price, p.stock, c.name AS category_name
            FROM product p
            JOIN category c ON p.category_id = c.category_id
            WHERE p.product_id = @product_id AND p.is_active = TRUE",
            ("product_id", productId));

        return row is null ? Resp.Fail("상품을 찾을 수 없습니다.") : Resp.Ok(row);
    }
}
