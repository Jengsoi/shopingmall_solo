using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>
/// 쇼핑몰 서버 (server.py, 기본 포트 5000).
/// 연결 하나당 세션 하나를 들고 있고, 로그인에 성공하면 이후 요청의 member_id 는
/// 클라이언트가 보낸 값이 아니라 항상 세션의 값으로 채워진다.
/// </summary>
public sealed class MallServer : TcpServerBase
{
    private readonly Dictionary<string, Handler> _handlers = new();

    public MallServer(string host, int port) : base("쇼핑몰", host, port)
    {
        AuthHandlers.Register(_handlers);
        ProductHandlers.Register(_handlers);
        CartHandlers.Register(_handlers);
        BoardHandlers.Register(_handlers);
    }

    public int HandlerCount => _handlers.Count;

    protected override async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var channel = new MessageChannel(client.GetStream());
        var session = new Session();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonObject? request = await channel.ReadAsync(ct);
                if (request is null)
                    break;

                JsonObject response = await ProcessRequestAsync(request, session);
                await channel.WriteAsync(response, ct);
            }
        }
        catch (ProtocolException e)
        {
            Console.WriteLine($"[{Name}] 프로토콜 오류: {e.Message}");
            try { await channel.WriteAsync(Resp.Fail(e.Message), ct); } catch { /* 이미 끊김 */ }
        }
    }

    public async Task<JsonObject> ProcessRequestAsync(JsonObject request, Session session)
    {
        string? action = request.Str("action");
        if (action is null || !_handlers.TryGetValue(action, out var handler))
            return Resp.Fail($"알 수 없는 action: {action ?? "None"}");

        // 클라이언트가 member_id 를 마음대로 보내 다른 회원 행세를 하지 못하게,
        // 요청의 member_id 는 지우고 로그인 세션의 값만 넣는다.
        request.Remove("member_id");
        if (session.MemberId is long memberId)
            request["member_id"] = memberId;

        // 관리자 전용 action 가드
        if ((action.StartsWith("admin_") || action.StartsWith("stats_")) && !session.IsAdmin)
            return Resp.Fail("관리자 권한이 필요합니다.");

        try
        {
            JsonObject response = await Db.RunAsync(db => handler(db, request, session));

            if (response.IsOk())
            {
                if (action == "login" && response.Obj("data") is { } data)
                {
                    session.MemberId = data.Int("member_id");
                    session.Role = data.Str("role");
                }
                else if (action is "logout" or "member_withdraw")
                {
                    session.Clear();
                }
            }

            return response;
        }
        catch (BusinessException e)
        {
            return Resp.Fail(e.Message);
        }
        catch (Exception e)
        {
            // 내부 오류 내용(SQL 등)은 서버 로그에만 남기고 클라이언트에는 알리지 않는다.
            Console.WriteLine($"[{Name}] {action} 처리 오류: {e}");
            return Resp.Fail("서버에서 요청을 처리하는 중 오류가 발생했습니다.");
        }
    }
}
