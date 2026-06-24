using System.Runtime.InteropServices;

namespace Pomodoro.App;

internal sealed class SystemSleepGuard : IDisposable
{
    private const ExecutionState AwakeState =
        ExecutionState.EsContinuous |
        ExecutionState.EsSystemRequired |
        ExecutionState.EsDisplayRequired;

    private bool _active;

    public SystemSleepGuard()
    {
        _active = SetThreadExecutionState(AwakeState) != 0;
    }

    public void Dispose()
    {
        if (_active)
        {
            SetThreadExecutionState(ExecutionState.EsContinuous);
            _active = false;
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
