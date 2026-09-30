// ============================================================================
// 서버 프로그램 진입점
//
// 한 프로세스 안에서 TCP 서버 3개를 동시에 띄운다.
//   쇼핑몰 서버   (5000) : 고객용. 회원·상품·장바구니·주문·게시판
//   재고관리 서버 (6000) : 관리자용. 카테고리·상품·재고, 재고 이력, 주문 관리
//   대시보드 서버 (6001) : 관리자용. 매출 통계
//
// 사용법
//   dotnet run --project src/ShoppingMall.Server                 # 세 서버 모두 실행
//   dotnet run --project src/ShoppingMall.Server -- --only mall  # mall | inventory | dashboard 중 하나만
//
// 환경변수
//   SHOP_BIND (기본 127.0.0.1), SHOP_MALL_PORT (5000), SHOP_INVENTORY_PORT (6000), SHOP_DASHBOARD_PORT (6001)
//   SHOP_DB_HOST / SHOP_DB_PORT / SHOP_DB_USER / SHOP_DB_PASSWORD / SHOP_DB_NAME  (DbConfig 참고)
// ============================================================================
using ShoppingMall.Server;
using ShoppingMall.Server.Admin;
using ShoppingMall.Server.Mall;

// 환경변수가 없거나 비어 있으면 기본값을 쓴다.
static string Env(string name, string fallback)
{
    var v = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrEmpty(v) ? fallback : v;
}

// 명령줄 인자에서 "--only <이름>" 을 찾는다. (서버 하나만 따로 띄워 디버깅할 때 사용)
string? only = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--only" && i + 1 < args.Length)
        only = args[++i];
}

// 기본값 127.0.0.1 은 이 컴퓨터 안에서만 접속할 수 있다는 뜻. 다른 PC 에서 접속하게 하려면 0.0.0.0 으로 바꾼다.
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

// Ctrl+C 를 누르면 프로세스를 바로 죽이지 않고, 취소 신호를 보내 각 서버가 정리하고 끝나게 한다.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n서버 종료 요청을 받았습니다.");
    cts.Cancel();
};

try
{
    // 세 서버를 동시에 실행하고, 모두 끝날 때(= 종료 요청)까지 기다린다.
    await Task.WhenAll(servers.Select(s => s.RunAsync(cts.Token)));
}
catch (System.Net.Sockets.SocketException e)
{
    // 대표적인 원인: 포트를 이미 다른 프로그램이 쓰고 있음
    Console.Error.WriteLine($"[서버 실행 오류] {e.Message}");
    return 1;
}

return 0;
