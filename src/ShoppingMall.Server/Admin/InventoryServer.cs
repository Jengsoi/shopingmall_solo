using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Server.Admin;

/// <summary>
/// 재고관리 서버 (inventory_server.py, 기본 포트 6000).
/// 요청 형식: {"type": "...", ...}  /  응답 형식: {"type": "...", "success": bool, "message": "..."}
/// 연결 후 먼저 {"type":"login","login_id":..,"password":..} 로 관리자 인증을 해야 한다.
/// </summary>
public sealed class InventoryServer : TcpServerBase
{
    private readonly InventoryService _inventory = new();

    public InventoryServer(string host, int port) : base("재고관리", host, port) { }

    protected override async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var channel = new MessageChannel(client.GetStream());
        bool authenticated = false;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonObject? request = await channel.ReadAsync(ct);
                if (request is null)
                    break;

                string type = request.Str("type") ?? "";
                JsonObject response;

                if (type.Length == 0)
                {
                    response = Error("요청 타입이 필요합니다.");
                }
                else if (type == "login")
                {
                    var (ok, message) = await AdminAuth.VerifyAsync(
                        (request.Str("login_id") ?? "").Trim(), request.Str("password") ?? "");
                    authenticated = ok;
                    response = new JsonObject
                    {
                        ["type"] = "login",
                        ["success"] = ok,
                        ["message"] = ok ? "로그인에 성공했습니다." : message,
                    };
                }
                else if (type == "logout")
                {
                    authenticated = false;
                    response = new JsonObject { ["type"] = "logout", ["success"] = true, ["message"] = "로그아웃되었습니다." };
                }
                else if (InventoryService.RequestTypes.Contains(type))
                {
                    response = authenticated
                        ? await _inventory.HandleAsync(request)
                        : new JsonObject { ["type"] = type, ["success"] = false, ["message"] = "로그인이 필요합니다." };
                }
                else
                {
                    response = Error($"지원하지 않는 요청입니다: {type}");
                }

                await channel.WriteAsync(response, ct);
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
