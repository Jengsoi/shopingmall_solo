using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Server.Admin;

/// <summary>
/// 대시보드 서버 (기본 포트 6001). 관리자 화면의 매출 현황 탭이 접속한다.
///
/// 대화 순서
///   1) 클라이언트 → {"type":"login","login_id":..,"password":..}   서버 → {"type":"login","success":..}
///   2) 클라이언트 → {"type":"data","start":"2026-09-01 00:00:00","end":"2026-09-30 23:59:59"}
///      서버 → 응답 3개를 차례로 보낸다.
///        {"type":"total_sales",    "content":[{"total_sales":"108000"}]}
///        {"type":"product_top5",   "content":[{"product_name":..,"total_sales":..}, ...]}
///        {"type":"category_sales", "content":[{"name":..,"total_sales":..}, ...]}
/// 오류가 나면 {"type":"error", "message": ...} 하나만 보낸다.
/// </summary>
public sealed class DashboardServer : TcpServerBase
{
    private readonly DashboardService _service = new();

    public DashboardServer(string host, int port) : base("대시보드", host, port) { }

    protected override async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var channel = new MessageChannel(client.GetStream());
        bool authenticated = false; // 이 연결이 관리자 로그인을 마쳤는지

        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonObject? request = await channel.ReadAsync(ct);
                if (request is null)
                    break;

                string type = request.Str("type") ?? "";

                if (type == "login")
                {
                    var (ok, message, _) = await AdminAuth.VerifyAsync(
                        (request.Str("login_id") ?? "").Trim(), request.Str("password") ?? "");
                    authenticated = ok;
                    await channel.WriteAsync(new JsonObject
                    {
                        ["type"] = "login",
                        ["success"] = ok,
                        ["message"] = ok ? "로그인에 성공했습니다." : message,
                    }, ct);
                    continue;
                }

                if (type != "data")
                {
                    await channel.WriteAsync(Error($"지원하지 않는 요청입니다: {type}"), ct);
                    continue;
                }

                if (!authenticated)
                {
                    await channel.WriteAsync(Error("로그인이 필요합니다."), ct);
                    continue;
                }

                string? start = request.Str("start");
                string? end = request.Str("end");
                if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
                {
                    await channel.WriteAsync(Error("조회 기간(start, end)이 필요합니다."), ct);
                    continue;
                }

                // 통계 3종을 각각 조회해서 하나씩 보낸다. (클라이언트는 3개를 모두 받은 뒤 화면을 그린다)
                await channel.WriteAsync(new JsonObject
                {
                    ["type"] = "total_sales",
                    ["content"] = await _service.GetTotalSalesAsync(start, end),
                }, ct);
                await channel.WriteAsync(new JsonObject
                {
                    ["type"] = "product_top5",
                    ["content"] = await _service.GetProductTop5Async(start, end),
                }, ct);
                await channel.WriteAsync(new JsonObject
                {
                    ["type"] = "category_sales",
                    ["content"] = await _service.GetCategorySalesAsync(start, end),
                }, ct);
            }
        }
        catch (ProtocolException e)
        {
            Console.WriteLine($"[{Name}] 프로토콜 오류: {e.Message}");
            try { await channel.WriteAsync(Error(e.Message), ct); } catch { /* 이미 끊김 */ }
        }
    }

    private static JsonObject Error(string message) =>
        new() { ["type"] = "error", ["success"] = false, ["message"] = message };
}
