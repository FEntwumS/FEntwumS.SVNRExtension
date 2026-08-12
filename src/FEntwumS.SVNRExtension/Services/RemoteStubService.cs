using System.Net;
using System.Net.Sockets;
using FEntwumS.SVNRExtension.Rsp;
using FEntwumS.SVNRExtension.Sbdp;

namespace FEntwumS.SVNRExtension.Services;

public sealed class RemoteStubService : IDisposable
{
    private const string TargetDescriptionFile = "target.xml";
    private const int LoadProgramLockTimeoutMilliseconds = 500;

    private readonly Lock _serialGate = new();

    private ISbdpTransport? _transport;
    private SvnrBootloaderClient? _client;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationToken;
    private Task? _sessionLoop;
    private string _targetDescription = string.Empty;

    public int Port { get; private set; }

    public bool IsRunning => _sessionLoop is { IsCompleted: false };

    public Action<bool, string>? TraceRsp { get; set; }

    public int Start(ISbdpTransport transport, int tcpPort = 0)
    {
        if (IsRunning) throw new InvalidOperationException("Der Stub laeuft bereits.");

        _targetDescription = ReadTargetDescription();
        _transport = transport;
        _client = new SvnrBootloaderClient(transport);

        if (!_client.TestCommunication())
            throw new InvalidOperationException("Am seriellen Port hat kein SVNR geantwortet.");

        _client.SwitchToDebug();
        _client.DebugReset();

        _listener = new TcpListener(IPAddress.Loopback, tcpPort);
        _listener.Start(1);
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _cancellationToken = new CancellationTokenSource();
        _sessionLoop = Task.Run(() => AcceptSessions(_cancellationToken.Token));

        return Port;
    }

    public void LoadProgram(byte[] image)
    {
        var client = _client ?? throw new InvalidOperationException("Der Stub laeuft nicht.");

        if (!_serialGate.TryEnter(LoadProgramLockTimeoutMilliseconds))
            throw new InvalidOperationException("Die serielle Verbindung ist belegt.");

        try
        {
            client.Bootload(image);
            client.SwitchToDebug();
            client.DebugReset();
        }
        finally
        {
            _serialGate.Exit();
        }
    }

    public void Stop()
    {
        _cancellationToken?.Cancel();
        _listener?.Stop();

        try
        {
            _sessionLoop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _transport?.Dispose();

        _sessionLoop = null;
        _listener = null;
        _transport = null;
        _client = null;
        Port = 0;
    }

    public void Dispose()
    {
        Stop();
        _cancellationToken?.Dispose();
        _cancellationToken = null;
    }

    private void AcceptSessions(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient connection;

            try
            {
                connection = _listener!.AcceptTcpClient();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException
                                                  or InvalidOperationException)
            {
                return;
            }

            using (connection)
            using (var networkStream = connection.GetStream())
            {
                RunSession(networkStream, token);
            }
        }
    }

    private void RunSession(NetworkStream networkStream, CancellationToken token)
    {
        var stream = new RspStream(networkStream) { Trace = TraceRsp };
        var processor = new RspCommandProcessor(_client!, _targetDescription, stream.TryConsumeInterrupt);

        while (!token.IsCancellationRequested)
        {
            string? command;

            try
            {
                command = stream.ReadCommand();
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                return;
            }

            if (command is null) return;

            string response;
            lock (_serialGate)
            {
                response = processor.Process(command);
            }

            try
            {
                stream.Send(response);
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static string ReadTargetDescription()
    {
        var directory = Path.GetDirectoryName(typeof(RemoteStubService).Assembly.Location)!;
        var path = Path.Combine(directory, "Assets", TargetDescriptionFile);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Die Zielbeschreibung fehlt: {path}", path);

        return File.ReadAllText(path);
    }
}
