using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

/// <summary>연결 실패·서버 거절 등 통신 문제. NetworkClient 내부에서만 쓰고 밖으로는 실패 응답으로 바꿔서 내보낸다.</summary>
public sealed class NetworkException : Exception
{
    public NetworkException(string message) : base(message) { }
}

/// <summary>
/// 서버 하나와의 연결. 요청을 한 번에 하나씩(순서대로) 보내고 응답을 받는다.
/// 처음 요청할 때 연결하고, 끊어졌으면 다음 요청 때 다시 연결한다.
/// 통신 오류는 예외 대신 실패 응답으로 돌려주므로 화면 코드에서 try/catch 가 필요 없다.
///
/// 쇼핑 화면은 쇼핑몰 서버용 1개, 관리자 화면은 재고관리·대시보드 서버용 2개를 만들어 쓴다.
/// </summary>
public sealed class NetworkClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;

    // 연결 하나로 요청을 주고받으므로, 두 요청이 동시에 나가면 응답 순서가 뒤섞인다.
    // 그래서 "요청 보내기 → 응답 받기" 한 쌍이 끝날 때까지 다음 요청은 기다리게 한다.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _tcp;
    private MessageChannel? _channel;

    public NetworkClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <summary>연결 직후 실행할 절차(예: 관리자 로그인). 오류 메시지를 반환하고, 성공이면 null.</summary>
    public Func<MessageChannel, Task<string?>>? Handshake { get; set; }

    /// <summary>
    /// 관리자 서버(재고관리/대시보드)용: 연결할 때마다 관리자 로그인을 먼저 한다.
    /// 연결이 끊겼다가 다시 붙어도 자동으로 다시 로그인하므로, 화면 코드는 인증을 신경 쓰지 않아도 된다.
    /// </summary>
    public static NetworkClient ForAdmin(string host, int port, string loginId, string password)
    {
        return new NetworkClient(host, port)
        {
            Handshake = async channel =>
            {
                await channel.WriteAsync(new JsonObject
                {
                    ["type"] = "login",
                    ["login_id"] = loginId,
                    ["password"] = password,
                });

                JsonObject? reply = await channel.ReadAsync();
                if (reply is null)
                    return "서버가 연결을 닫았습니다.";

                return reply.Bool("success") == true ? null : reply.Str("message") ?? "관리자 로그인에 실패했습니다.";
            }
        };
    }

    /// <summary>
    /// 실패 응답. 쇼핑몰 서버는 status, 관리자 서버는 success/type 으로 성공 여부를 알려 주므로,
    /// 세 필드를 모두 채워서 어느 서버의 응답이든 같은 방식으로 검사할 수 있게 한다.
    /// </summary>
    public static JsonObject FailResponse(string message) => new()
    {
        ["status"] = "fail",
        ["success"] = false,
        ["type"] = "error",
        ["message"] = message,
    };

    /// <summary>요청 하나에 응답 하나.</summary>
    public async Task<JsonObject> RequestAsync(JsonObject payload)
    {
        var list = await ExchangeAsync(payload, 1);
        return list[0];
    }

    /// <summary>요청 하나에 응답이 expected 개 돌아오는 경우(대시보드). 오류 응답이 오면 그 하나만 돌려준다.</summary>
    public async Task<List<JsonObject>> ExchangeAsync(JsonObject payload, int expected)
    {
        await _gate.WaitAsync();
        try
        {
            await EnsureConnectedAsync();
            await _channel!.WriteAsync(payload);

            var responses = new List<JsonObject>();
            for (int i = 0; i < expected; i++)
            {
                JsonObject? reply = await _channel.ReadAsync();
                if (reply is null)
                    throw new NetworkException("서버가 연결을 닫았습니다.");

                responses.Add(reply);
                if (reply.Str("type") == "error")
                    break; // 서버가 오류를 보내면 나머지 응답은 오지 않는다
            }
            return responses;
        }
        catch (NetworkException e)
        {
            // 연결 상태가 불확실하므로 끊어 두고, 다음 요청 때 새로 연결한다.
            Disconnect();
            return new List<JsonObject> { FailResponse(e.Message) };
        }
        catch (Exception e) when (e is IOException or SocketException or ProtocolException or ObjectDisposedException or OperationCanceledException)
        {
            // 통신 중 흔히 생기는 예외만 잡는다. 그 외(프로그램 버그 등)는 그대로 올려 보내 문제를 숨기지 않는다.
            Disconnect();
            return new List<JsonObject> { FailResponse("서버와 통신하지 못했습니다: " + e.Message) };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>연결되어 있지 않으면 새로 연결한다. (5초 안에 연결되지 않으면 실패)</summary>
    private async Task EnsureConnectedAsync()
    {
        if (_tcp is { Connected: true } && _channel is not null)
            return;

        Disconnect();

        var tcp = new TcpClient();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await tcp.ConnectAsync(_host, _port, timeout.Token);
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException)
        {
            tcp.Dispose();
            throw new NetworkException($"서버({_host}:{_port})에 연결할 수 없습니다.");
        }

        var channel = new MessageChannel(tcp.GetStream());
        _tcp = tcp;
        _channel = channel;

        // 관리자 서버라면 여기서 로그인까지 마친다.
        if (Handshake is not null)
        {
            string? error = await Handshake(channel);
            if (error is not null)
                throw new NetworkException(error);
        }
    }

    public void Disconnect()
    {
        try { _channel?.Dispose(); } catch { /* 무시 */ }
        try { _tcp?.Dispose(); } catch { /* 무시 */ }
        _channel = null;
        _tcp = null;
    }

    public void Dispose()
    {
        Disconnect();
        _gate.Dispose();
    }
}
