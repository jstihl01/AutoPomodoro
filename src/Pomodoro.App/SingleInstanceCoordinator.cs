using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Pomodoro.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    // The installer and releases before 2.0.2 use this session-local mutex.
    internal const string DefaultMutexName = "Local\\Pomodoro-8146708D-9AF6-43D9-87B8-76F527A8AC19";
    private const byte Activate = 1;
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly uint _sessionId;
    private readonly CancellationTokenSource _shutdown = new();
    private NamedPipeServerStream? _server;
    private Task? _listener;
    private bool _ownsMutex;

    public SingleInstanceCoordinator(string mutexName = DefaultMutexName)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new UnauthorizedAccessException("No se pudo identificar la cuenta de Windows.");
        using var process = Process.GetCurrentProcess();
        _sessionId = (uint)process.SessionId;
        _pipeName = $"{mutexName.Replace('\\', '-')}-{user.Value}-{_sessionId}";

        var security = new MutexSecurity();
        security.SetOwner(user);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new MutexAccessRule(user, MutexRights.FullControl, AccessControlType.Allow));
        _mutex = MutexAcl.Create(false, mutexName, out _, security);
        if (!user.Equals(_mutex.GetAccessControl().GetOwner(typeof(SecurityIdentifier))))
        {
            _mutex.Dispose();
            throw new UnauthorizedAccessException("El bloqueo de instancia pertenece a otra cuenta.");
        }
    }

    public bool AcquireOrActivate()
    {
        var time = Stopwatch.StartNew();
        do
        {
            try { _ownsMutex = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { _ownsMutex = true; }
            if (_ownsMutex) return true;
            if (RequestActivation().GetAwaiter().GetResult()) return false;
        }
        while (time.Elapsed < TimeSpan.FromSeconds(4));

        throw new TimeoutException("La otra instancia no ha confirmado la activación.");
    }

    public void StartListening(Func<CancellationToken, Task<bool>> activate)
    {
        if (!_ownsMutex || _server is not null) return;
        var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _server = server;
        var token = _shutdown.Token;
        _listener = Task.Run(() => Listen(server, activate, token));
    }

    private async Task<bool> RequestActivation()
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, TokenImpersonationLevel.Identification);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(180));
        try
        {
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            if (!GetNamedPipeServerSessionId(client.SafePipeHandle, out var session) || session != _sessionId)
                throw new UnauthorizedAccessException("El receptor de la activación pertenece a otra sesión.");

            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            await client.WriteAsync(new byte[] { Activate }, timeout.Token).ConfigureAwait(false);
            return await ReadActivation(client, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or IOException)
        {
            // The primary may still be starting or may have just released the mutex.
            return false;
        }
    }

    private async Task Listen(NamedPipeServerStream server, Func<CancellationToken, Task<bool>> activate, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                try
                {
                    using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(1));
                    var requestToken = requestTimeout.Token;
                    if (GetNamedPipeClientSessionId(server.SafePipeHandle, out var session) && session == _sessionId &&
                        await ReadActivation(server, requestToken).ConfigureAwait(false) &&
                        await activate(requestToken).ConfigureAwait(false))
                    {
                        await server.WriteAsync(new byte[] { Activate }, requestToken).ConfigureAwait(false);
                        // Disconnect can discard unread data. Give the client time to read and close.
                        await server.ReadAsync(new byte[1], requestToken).ConfigureAwait(false);
                    }
                }
                catch (Exception error) when (error is IOException or OperationCanceledException)
                {
                    if (!token.IsCancellationRequested)
                        Trace.TraceWarning("AutoPomodoro: activación local interrumpida (0x{0:X8})", error.HResult);
                }
                finally
                {
                    if (!token.IsCancellationRequested) server.Disconnect();
                }
            }
        }
        catch (Exception error) when (token.IsCancellationRequested && error is (OperationCanceledException or ObjectDisposedException or IOException)) { }
        catch (Exception error)
        {
            Trace.TraceWarning("AutoPomodoro: receptor de activación detenido ({0}, 0x{1:X8})", error.GetType().Name, error.HResult);
        }
    }

    private static async Task<bool> ReadActivation(PipeStream stream, CancellationToken token)
    {
        var message = new byte[1];
        return await stream.ReadAsync(message, token).ConfigureAwait(false) == 1 && message[0] == Activate;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _server?.Dispose();
        if (_ownsMutex) _mutex.ReleaseMutex();
        _mutex.Dispose();
        if (_listener is { } listener)
            _ = listener.ContinueWith(_ => _shutdown.Dispose(), TaskScheduler.Default);
        else
            _shutdown.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint sessionId);

    [DllImport("kernel32.dll")]
    private static extern bool GetNamedPipeServerSessionId(SafePipeHandle pipe, out uint sessionId);
}

