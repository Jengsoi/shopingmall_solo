using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;
using ShoppingMall.Server.Data;

namespace ShoppingMall.Server.Mall;

/// <summary>
/// 쇼핑몰 서버 (기본 포트 5000). 고객 화면이 접속하는 서버다.
///
/// 요청 형식: {"action": "login", ...필요한 값...}
/// 응답 형식: {"status": "success"|"fail", "data": ..., "message": ...}
///
/// action 이름으로 처리 함수(핸들러)를 찾아 실행하는 "라우터" 역할을 한다.
/// 연결 하나당 세션 하나를 들고 있고, 로그인에 성공하면 이후 요청의 member_id 는
/// 클라이언트가 보낸 값이 아니라 항상 세션의 값으로 채워진다.
/// </summary>
public sealed class MallServer : TcpServerBase
{
    // action 이름 → 처리 함수. 예: "cart_add" → CartHandlers.CartAddAsync
    private readonly Dictionary<string, Handler> _handlers = new();

    public MallServer(string host, int port) : base("쇼핑몰", host, port)
    {
        // 기능별 핸들러 묶음이 각자 자기 action 들을 등록한다.
        AuthHandlers.Register(_handlers);
        ProductHandlers.Register(_handlers);
        CartHandlers.Register(_handlers);
        BoardHandlers.Register(_handlers);
    }

    public int HandlerCount => _handlers.Count;

    /// <summary>클라이언트 한 명: 연결이 끊길 때까지 "요청 읽기 → 처리 → 응답 쓰기" 를 반복한다.</summary>
    protected override async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var channel = new MessageChannel(client.GetStream());
        var session = new Session(); // 이 연결의 로그인 상태 (처음엔 비로그인)

        try
        {
            while (!ct.IsCancellationRequested)
            {
                JsonObject? request = await channel.ReadAsync(ct);
                if (request is null)
                    break; // 클라이언트가 연결을 닫음

                JsonObject response = await ProcessRequestAsync(request, session);
                await channel.WriteAsync(response, ct);
            }
        }
        catch (ProtocolException e)
        {
            // 형식이 깨진 메시지: 이유를 알려 주고 연결을 끊는다.
            Console.WriteLine($"[{Name}] 프로토콜 오류: {e.Message}");
            try { await channel.WriteAsync(Resp.Fail(e.Message), ct); } catch { /* 이미 끊김 */ }
        }
    }

    /// <summary>요청 하나를 처리해 응답을 만든다. 모든 예외를 여기서 실패 응답으로 바꾼다.</summary>
    public async Task<JsonObject> ProcessRequestAsync(JsonObject request, Session session)
    {
        string? action = request.Str("action");
        if (action is null || !_handlers.TryGetValue(action, out var handler))
            return Resp.Fail($"알 수 없는 action: {action ?? "None"}");

        // 클라이언트가 member_id 를 마음대로 보내 다른 회원 행세를 하지 못하게,
        // 요청의 member_id 는 지우고 로그인 세션의 값만 넣는다.
        // 그래서 핸들러는 req.Int("member_id") 를 "지금 로그인한 사람" 으로 믿고 써도 된다.
        request.Remove("member_id");
        if (session.MemberId is long memberId)
            request["member_id"] = memberId;

        // 관리자 전용 action 가드 (이름이 admin_ / stats_ 로 시작하면 관리자만)
        if ((action.StartsWith("admin_") || action.StartsWith("stats_")) && !session.IsAdmin)
            return Resp.Fail("관리자 권한이 필요합니다.");

        try
        {
            // 요청 하나 = 트랜잭션 하나. 핸들러가 예외 없이 끝나면 커밋, 예외가 나면 롤백된다.
            JsonObject response = await Db.RunAsync(db => handler(db, request, session));

            // 로그인/로그아웃은 응답이 성공일 때만 세션 상태를 바꾼다.
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
            // 업무 규칙 위반("재고 부족" 등): 메시지를 그대로 전달
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
