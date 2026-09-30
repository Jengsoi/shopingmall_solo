using System.Globalization;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

/// <summary>재고관리 서버(포트 6000) API.</summary>
public sealed class InventoryApi
{
    private readonly NetworkClient _net;

    public InventoryApi(NetworkClient net)
    {
        _net = net;
    }

    public async Task<ApiResult<List<AdminCategory>>> CategoryListAsync() =>
        ApiResult<List<AdminCategory>>.From(
            await _net.RequestAsync(new JsonObject { ["type"] = "category_list" }),
            r => r.Arr("categories")?.Objects().Select(AdminCategory.Parse).ToList() ?? new List<AdminCategory>(),
            "카테고리 조회에 실패했습니다.");

    public async Task<ApiResult> CategoryAddAsync(string name) =>
        ApiResult.From(await _net.RequestAsync(new JsonObject { ["type"] = "category_add", ["name"] = name }),
            "카테고리 추가에 실패했습니다.");

    public async Task<ApiResult> CategoryUpdateAsync(long categoryId, string name, bool isActive) =>
        ApiResult.From(await _net.RequestAsync(new JsonObject
        {
            ["type"] = "category_update",
            ["category_id"] = categoryId,
            ["name"] = name,
            ["is_active"] = isActive,
        }), "카테고리 수정에 실패했습니다.");

    public async Task<ApiResult<List<AdminProduct>>> ProductListAsync() =>
        ApiResult<List<AdminProduct>>.From(
            await _net.RequestAsync(new JsonObject { ["type"] = "inventory_product_list" }),
            r => r.Arr("products")?.Objects().Select(AdminProduct.Parse).ToList() ?? new List<AdminProduct>(),
            "상품 조회에 실패했습니다.");

    private static JsonObject ProductRequest(string type, ProductInput p) => new()
    {
        ["type"] = type,
        ["category_id"] = p.CategoryId,
        ["name"] = p.Name,
        ["description"] = p.Description,
        ["color"] = p.Color,
        ["size"] = p.Size,
        ["price"] = p.Price,
        ["inventory"] = p.Stock,
    };

    public async Task<ApiResult> ProductAddAsync(ProductInput product) =>
        ApiResult.From(await _net.RequestAsync(ProductRequest("product_add", product)), "상품 추가에 실패했습니다.");

    /// <summary>수정은 기존 상품을 비활성화하고 새 상품을 만든다(이력 보존).</summary>
    public async Task<ApiResult> ProductUpdateAsync(long productId, ProductInput product)
    {
        var request = ProductRequest("product_update", product);
        request["product_id"] = productId;
        return ApiResult.From(await _net.RequestAsync(request), "상품 수정에 실패했습니다.");
    }
}

/// <summary>대시보드 서버(포트 6001) API.</summary>
public sealed class DashboardApi
{
    private readonly NetworkClient _net;

    public DashboardApi(NetworkClient net)
    {
        _net = net;
    }

    /// <summary>start/end 기간의 총 매출, 상품 TOP5, 카테고리별 매출을 조회한다.</summary>
    public async Task<ApiResult<DashboardData>> QueryAsync(DateTime start, DateTime end)
    {
        var request = new JsonObject
        {
            ["type"] = "data",
            ["start"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00",
            ["end"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 23:59:59",
        };

        List<JsonObject> responses = await _net.ExchangeAsync(request, 3);

        if (responses.Count == 1 && responses[0].Str("type") == "error")
        {
            return new ApiResult<DashboardData>
            {
                Ok = false,
                Message = responses[0].Str("message") ?? "대시보드 조회에 실패했습니다.",
            };
        }

        long total = 0;
        var top5 = new List<SalesEntry>();
        var categories = new List<SalesEntry>();

        foreach (var response in responses)
        {
            JsonArray content = response.Arr("content") ?? new JsonArray();
            switch (response.Str("type"))
            {
                case "total_sales":
                    total = content.Objects().Select(o => ParseAmount(o.Str("total_sales"))).FirstOrDefault();
                    break;
                case "product_top5":
                    top5 = content.Objects().Select(o => new SalesEntry(o.Str("product_name") ?? "unknown", ParseAmount(o.Str("total_sales")))).ToList();
                    break;
                case "category_sales":
                    categories = content.Objects().Select(o => new SalesEntry(o.Str("name") ?? "unknown", ParseAmount(o.Str("total_sales")))).ToList();
                    break;
            }
        }

        return new ApiResult<DashboardData> { Ok = true, Value = new DashboardData(total, top5, categories) };
    }

    /// <summary>SUM 결과 문자열(예: "123000" 또는 null)을 금액으로 바꾼다.</summary>
    private static long ParseAmount(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? (long)d : 0;
}
