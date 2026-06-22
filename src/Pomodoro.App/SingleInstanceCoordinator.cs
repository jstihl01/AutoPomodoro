using System.Threading;

namespace Pomodoro.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\Pomodoro-8146708D-9AF6-43D9-87B8-76F527A8AC19";
    private const string EventName = "Local\\Pomodoro-Activate-8146708D-9AF6-43D9-87B8-76F527A8AC19";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(true, MutexName, out var isPrimary);
        IsPrimary = isPrimary;
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    public bool IsPrimary { get; }
    public event EventHandler? ActivationRequested;

    public void SignalPrimary() => _activationEvent.Set();

    public void StartListening()
    {
        if (!IsPrimary || _listener is not null)
        {
            return;
        }

        _listener = Task.Run(() =>
        {
            var handles = new[] { _activationEvent, _cancellation.Token.WaitHandle };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _listener?.Wait(TimeSpan.FromSeconds(1));
        _activationEvent.Dispose();
        _cancellation.Dispose();
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}

