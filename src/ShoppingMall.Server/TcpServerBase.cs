using System.Net;
using System.Net.Sockets;

namespace ShoppingMall.Server;

/// <summary>
/// TCP 서버 3개(쇼핑몰·재고관리·대시보드)의 공통 뼈대.
/// 접속을 기다렸다가, 클라이언트가 들어올 때마다 별도 작업(Task)으로 넘겨 동시에 여러 명을 처리한다.
/// 실제로 요청을 어떻게 처리할지는 하위 클래스가 HandleClientAsync 에서 정한다. (템플릿 메서드 패턴)
/// </summary>
public abstract class TcpServerBase
{
    protected TcpServerBase(string name, string host, int port)
    {
        Name = name;
        Host = host;
        Port = port;
    }

    /// <summary>로그에 찍을 서버 이름 (예: "쇼핑몰")</summary>
    public string Name { get; }
    public string Host { get; }
    public int Port { get; }

    /// <summary>클라이언트 한 명과의 연결을 처리한다. 이 메서드가 끝나면 연결이 닫힌다.</summary>
    protected abstract Task HandleClientAsync(TcpClient client, CancellationToken ct);

    /// <summary>종료 신호(ct)가 올 때까지 접속을 계속 받는다.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Parse(Host), Port);
        listener.Start();
        Console.WriteLine($"[{Name}] 서버 시작 ({Host}:{Port})");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(ct);

                // 기다리지 않고(fire-and-forget) 다음 접속을 받으러 간다.
                // 그래서 한 클라이언트가 오래 걸려도 다른 클라이언트가 막히지 않는다.
                _ = Task.Run(() => RunClientAsync(client, ct), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // 종료 요청 (Ctrl+C)
        }
        finally
        {
            listener.Stop();
            Console.WriteLine($"[{Name}] 서버 종료");
        }
    }

    /// <summary>
    /// 클라이언트 하나를 처리하는 감싸기 메서드. 어떤 예외가 나도 서버 전체가 죽지 않도록 여기서 잡아 로그만 남긴다.
    /// </summary>
    private async Task RunClientAsync(TcpClient client, CancellationToken ct)
    {
        string peer = client.Client.RemoteEndPoint?.ToString() ?? "?";
        Console.WriteLine($"[{Name}] 연결됨 {peer}");

        try
        {
            using (client)
                await HandleClientAsync(client, ct);
        }
        catch (OperationCanceledException) { }
        catch (IOException e)
        {
            // 상대가 갑자기 연결을 끊은 경우 등
            Console.WriteLine($"[{Name}] 연결 오류 {peer}: {e.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{Name}] 처리 오류 {peer}: {e}");
        }
        finally
        {
            Console.WriteLine($"[{Name}] 연결 종료 {peer}");
        }
    }
}
