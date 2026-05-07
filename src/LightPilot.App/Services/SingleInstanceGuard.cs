using System.Threading;

namespace LightPilot.App.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showWindowEvent;
    private CancellationTokenSource? _listenerCts;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle showWindowEvent)
    {
        _mutex = mutex;
        _showWindowEvent = showWindowEvent;
    }

    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        var mutex = new Mutex(initiallyOwned: true, "Local\\LightPilot.Desktop.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            mutex.Dispose();
            guard = null;
            return false;
        }

        var showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LightPilot.Desktop.ShowWindow");
        guard = new SingleInstanceGuard(mutex, showWindowEvent);
        return true;
    }

    public void StartActivationListener(Action activate)
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_showWindowEvent.WaitOne(TimeSpan.FromMilliseconds(500)))
                    {
                        activate();
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _showWindowEvent.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var showWindowEvent = EventWaitHandle.OpenExisting("Local\\LightPilot.Desktop.ShowWindow");
            showWindowEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }
}
