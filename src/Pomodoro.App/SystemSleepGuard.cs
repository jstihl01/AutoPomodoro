using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pomodoro.App;

internal sealed class SystemSleepGuard : IDisposable
{
    private const ExecutionState AwakeState =
        ExecutionState.EsContinuous |
        ExecutionState.EsSystemRequired |
        ExecutionState.EsDisplayRequired;

    public bool Active { get; private set; }

    public void TryAcquire()
    {
        if (Active) return;
        Active = SetThreadExecutionState(AwakeState) != 0;
        if (!Active) Trace.TraceWarning("AutoPomodoro: Windows rechazo la solicitud de energia.");
    }

    public void Dispose()
    {
        if (Active)
        {
            if (SetThreadExecutionState(ExecutionState.EsContinuous) == 0)
                Trace.TraceWarning("AutoPomodoro: no se pudo liberar la solicitud de energia.");
            Active = false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [Flags]
    private enum ExecutionState : uint
    {
        EsContinuous = 0x80000000,
        EsSystemRequired = 0x00000001,
        EsDisplayRequired = 0x00000002
    }
}
