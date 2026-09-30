using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShoppingMall.Protocol;

/// <summary>
/// 통신 규칙(프로토콜)을 어긴 메시지를 받았을 때 던지는 예외.
/// 예: JSON 형식이 깨졌거나, 객체({...})가 아니거나, 메시지가 너무 큰 경우.
/// 서버는 이 예외를 받으면 오류 응답을 한 번 보내고 연결을 끊는다.
/// </summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
}

/// <summary>
/// TCP 스트림 위에서 "JSON 한 줄 = 메시지 하나" 로 주고받는 채널. (서버·클라이언트 공용)
///
/// TCP 는 바이트가 이어서 흘러오는 스트림이라 "메시지가 어디서 끝나는지" 를 따로 정해야 한다.
/// 이 프로젝트는 메시지 끝에 줄바꿈(\n)을 붙이는 방식을 쓴다.
///   보낼 때:  {"action":"login",...}\n
///   받을 때:  \n 이 나올 때까지 모았다가 그 앞부분을 JSON 으로 해석
/// JSON 문자열 안의 줄바꿈은 항상 "\n" 두 글자로 이스케이프되므로, 실제 줄바꿈 바이트는
/// 메시지 경계에서만 나타난다. 그래서 구분자로 안전하게 쓸 수 있다.
/// </summary>
public sealed class MessageChannel : IDisposable
{
    /// <summary>메시지 하나의 최대 크기(10MB). 끝없이 줄바꿈 없는 데이터를 보내 메모리를 고갈시키는 것을 막는다.</summary>
    public const int MaxMessageSize = 10 * 1024 * 1024;

    // 한글을 한 같은 형태로 바꾸지 않고 그대로 보낸다. (로그와 패킷을 사람이 읽기 쉽게)
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly Stream _stream;

    // 여러 작업이 동시에 WriteAsync 를 부르면 두 메시지의 바이트가 섞일 수 있어서, 쓰기는 한 번에 하나씩만 한다.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // 수신 버퍼. [_start, _end) 구간이 아직 처리하지 않은 데이터다.
    // 한 번의 ReadAsync 로 메시지 여러 개가 들어오거나, 메시지 하나가 여러 번에 나눠 들어올 수 있어서 버퍼에 모아 둔다.
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
            // 1) 버퍼 안에 이미 완성된 줄(\n 까지)이 있으면 그 줄을 메시지로 꺼낸다.
            int nl = Array.IndexOf(_buf, (byte)'\n', _start, _end - _start);
            if (nl >= 0)
            {
                string line = Encoding.UTF8.GetString(_buf, _start, nl - _start);
                _start = nl + 1; // 다음 메시지는 줄바꿈 바로 뒤부터

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

                // 요청/응답은 항상 {...} 객체여야 한다. 배열이나 숫자 하나만 오면 규칙 위반.
                if (node is not JsonObject obj)
                    throw new ProtocolException("요청 형식이 올바르지 않습니다.");

                return obj;
            }

            // 2) 아직 줄이 완성되지 않았다. 처리한 앞부분을 버리고 남은 데이터를 버퍼 맨 앞으로 당긴다.
            if (_start > 0)
            {
                Buffer.BlockCopy(_buf, _start, _buf, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }

            // 3) 버퍼가 꽉 찼으면 두 배로 늘린다. (최대 크기를 넘으면 거부)
            if (_end == _buf.Length)
            {
                if (_buf.Length >= MaxMessageSize)
                    throw new ProtocolException("허용 가능한 메시지 크기를 초과했습니다.");

                Array.Resize(ref _buf, Math.Min(_buf.Length * 2, MaxMessageSize));
            }

            // 4) 소켓에서 더 읽어 버퍼 뒤에 이어 붙인다. 0 바이트를 읽었다는 것은 상대가 연결을 닫았다는 뜻.
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
            await _stream.FlushAsync(ct); // 버퍼에 쌓아 두지 말고 바로 보낸다.
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
