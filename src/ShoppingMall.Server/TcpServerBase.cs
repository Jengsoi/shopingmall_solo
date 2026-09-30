using System.Net;
using System.Net.Sockets;

namespace ShoppingMall.Server;

/// <summary>접속을 받아 클라이언트마다 별도 작업(Task)으로 처리하는 TCP 서버 공통 뼈대.</summary>
public abstract class TcpServerBase
{
    protected TcpServerBase(string name, string host, int port)
    {
        Name = name;
        Host = host;
        Port = port;
    }

    public string Name { get; }
    public string Host { get; }
    public int Port { get; }

    protected abstract Task HandleClientAsync(TcpClient client, CancellationToken ct);

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
                _ = Task.Run(() => RunClientAsync(client, ct), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            // 종료 요청
        }
        finally
        {
            listener.Stop();
            Console.WriteLine($"[{Name}] 서버 종료");
        }
    }

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
