using ShoppingMall.Server;
using ShoppingMall.Server.Admin;
using ShoppingMall.Server.Mall;

// 사용법
//   dotnet run --project src/ShoppingMall.Server                 # 세 서버 모두 실행
//   dotnet run --project src/ShoppingMall.Server -- --only mall  # mall | inventory | dashboard 중 하나만
//
// 환경변수
//   SHOP_BIND (기본 127.0.0.1), SHOP_MALL_PORT (5000), SHOP_INVENTORY_PORT (6000), SHOP_DASHBOARD_PORT (6001)
//   SHOP_DB_HOST / SHOP_DB_PORT / SHOP_DB_USER / SHOP_DB_PASSWORD / SHOP_DB_NAME

static string Env(string name, string fallback)
{
    var v = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrEmpty(v) ? fallback : v;
}

string? only = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--only" && i + 1 < args.Length)
        only = args[++i];
}

string host = Env("SHOP_BIND", "127.0.0.1");
int mallPort = int.Parse(Env("SHOP_MALL_PORT", "5000"));
int inventoryPort = int.Parse(Env("SHOP_INVENTORY_PORT", "6000"));
int dashboardPort = int.Parse(Env("SHOP_DASHBOARD_PORT", "6001"));

var servers = new List<TcpServerBase>();
if (only is null or "mall") servers.Add(new MallServer(host, mallPort));
if (only is null or "inventory") servers.Add(new InventoryServer(host, inventoryPort));
if (only is null or "dashboard") servers.Add(new DashboardServer(host, dashboardPort));

if (servers.Count == 0)
{
    Console.Error.WriteLine($"알 수 없는 --only 값: {only} (mall | inventory | dashboard)");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n서버 종료 요청을 받았습니다.");
    cts.Cancel();
};

try
{
    await Task.WhenAll(servers.Select(s => s.RunAsync(cts.Token)));
}
catch (System.Net.Sockets.SocketException e)
{
    Console.Error.WriteLine($"[서버 실행 오류] {e.Message}");
    return 1;
}

return 0;
