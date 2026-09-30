using System.Text;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>상품 페이지 (handlers/product_handler.py)</summary>
public static class ProductHandlers
{
    public static void Register(Dictionary<string, Handler> map)
    {
        map["category_list"] = CategoryListAsync;
        map["product_list"] = ProductListAsync;
        map["product_detail"] = ProductDetailAsync;
    }

    private static async Task<JsonObject> CategoryListAsync(SqlSession db, JsonObject req, Session s)
    {
        var rows = await db.RowsAsync(
            "SELECT category_id, name FROM category WHERE is_active = TRUE ORDER BY category_id");
        return Resp.Ok(Resp.Array(rows));
    }

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
            sql.Append(" AND p.name LIKE @keyword");
            args.Add(("keyword", $"%{keyword}%"));
        }

        sql.Append(" ORDER BY p.product_id");

        var rows = await db.RowsAsync(sql.ToString(), args.ToArray());
        return Resp.Ok(Resp.Array(rows));
    }

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
