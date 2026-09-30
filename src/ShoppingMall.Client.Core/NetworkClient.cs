using System.Net.Sockets;
using System.Text.Json.Nodes;
using ShoppingMall.Protocol;

namespace ShoppingMall.Client.Core;

public sealed class NetworkException : Exception
{
    public NetworkException(string message) : base(message) { }
}

/// <summary>
/// 서버 하나와의 연결. 요청을 한 번에 하나씩(순서대로) 보내고 응답을 받는다.
/// 처음 요청할 때 연결하고, 끊어졌으면 다음 요청 때 다시 연결한다.
/// 통신 오류는 예외 대신 실패 응답으로 돌려주므로 화면 코드에서 try/catch 가 필요 없다.
/// </summary>
public sealed class NetworkClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
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

    /// <summary>관리자 서버(재고관리/대시보드)용: 연결할 때마다 관리자 로그인을 먼저 한다.</summary>
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

    /// <summary>실패 응답. status/success 두 형식을 모두 채워서 어느 서버의 응답이든 같은 방식으로 검사할 수 있다.</summary>
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
                    break;
            }
            return responses;
        }
        catch (NetworkException e)
        {
            Disconnect();
            return new List<JsonObject> { FailResponse(e.Message) };
        }
        catch (Exception e) when (e is IOException or SocketException or ProtocolException or ObjectDisposedException or OperationCanceledException)
        {
            Disconnect();
            return new List<JsonObject> { FailResponse("서버와 통신하지 못했습니다: " + e.Message) };
        }
        finally
        {
            _gate.Release();
        }
    }

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
