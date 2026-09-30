using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShoppingMall.Protocol;

/// <summary>프로토콜 위반(JSON 형식 오류, 메시지 크기 초과 등).</summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
}

/// <summary>
/// 줄바꿈(\n)으로 메시지 경계를 구분하는 JSON 메시지 채널.
/// Python 버전(network_client.py / inventory_server.py)과 같은 선(line) 프로토콜이라
/// 기존 Python 클라이언트/서버와도 그대로 통신할 수 있다.
/// JSON 문자열 안의 줄바꿈은 항상 \n 으로 이스케이프되므로 구분자로 안전하다.
/// </summary>
public sealed class MessageChannel : IDisposable
{
    public const int MaxMessageSize = 10 * 1024 * 1024;

    // ensure_ascii=False 와 동일하게 한글을 \uXXXX 로 바꾸지 않는다.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private byte[] _buf = new byte[8192];
    private int _start;
    private int _end;

    public MessageChannel(Stream stream)
    {
        _stream = stream;
    }

    /// <summary>메시지 하나를 읽는다. 상대가 연결을 닫으면 null.</summary>
    public async Task<JsonObject?> ReadAsync(CancellationToken ct = default)
    {
        while (true)
        {
            int nl = Array.IndexOf(_buf, (byte)'\n', _start, _end - _start);
            if (nl >= 0)
            {
                string line = Encoding.UTF8.GetString(_buf, _start, nl - _start);
                _start = nl + 1;

                if (line.Trim().Length == 0)
                    continue; // 빈 줄은 무시

                JsonNode? node;
                try
                {
                    node = JsonNode.Parse(line);
                }
                catch (JsonException)
                {
                    throw new ProtocolException("JSON 형식이 올바르지 않습니다.");
                }

                if (node is not JsonObject obj)
                    throw new ProtocolException("요청 형식이 올바르지 않습니다.");

                return obj;
            }

            // 처리한 앞부분을 버리고 버퍼를 앞으로 당긴다.
            if (_start > 0)
            {
                Buffer.BlockCopy(_buf, _start, _buf, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }

            if (_end == _buf.Length)
            {
                if (_buf.Length >= MaxMessageSize)
                    throw new ProtocolException("허용 가능한 메시지 크기를 초과했습니다.");

                Array.Resize(ref _buf, Math.Min(_buf.Length * 2, MaxMessageSize));
            }

            int n = await _stream.ReadAsync(_buf.AsMemory(_end), ct);
            if (n == 0)
                return null;

            _end += n;
        }
    }

    /// <summary>메시지 하나를 JSON + \n 으로 전송한다.</summary>
    public async Task WriteAsync(JsonNode message, CancellationToken ct = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message.ToJsonString(WriteOptions) + "\n");

        await _writeLock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
        _stream.Dispose();
    }
}
