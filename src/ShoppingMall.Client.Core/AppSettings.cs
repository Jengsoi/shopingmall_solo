namespace ShoppingMall.Client.Core;

/// <summary>서버 주소. 환경변수(SHOP_HOST, SHOP_MALL_PORT, SHOP_INVENTORY_PORT, SHOP_DASHBOARD_PORT)로 바꿀 수 있다.</summary>
public static class AppSettings
{
    private static string Env(string name, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    public static string Host => Env("SHOP_HOST", "127.0.0.1");
    public static int MallPort => int.Parse(Env("SHOP_MALL_PORT", "5000"));
    public static int InventoryPort => int.Parse(Env("SHOP_INVENTORY_PORT", "6000"));
    public static int DashboardPort => int.Parse(Env("SHOP_DASHBOARD_PORT", "6001"));
}
