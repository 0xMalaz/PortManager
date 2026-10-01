using System.IO;

namespace PortManager.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
#if DEBUG
    private static readonly bool IsUiTest =
        (Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? string.Empty)
            .EndsWith(".UiTest", StringComparison.OrdinalIgnoreCase);
    private static readonly string MutexName = IsUiTest
        ? @"Local\PortManager.UiTest.SingleInstance.1"
        : @"Local\PortManager.SingleInstance.1";
    private static readonly string ActivationEventName = IsUiTest
        ? @"Local\PortManager.UiTest.Activate.1"
        : @"Local\PortManager.Activate.1";
#else
    private const string MutexName = @"Local\PortManager.SingleInstance.1";
    private const string ActivationEventName = @"Local\PortManager.Activate.1";
#endif

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly RegisteredWaitHandle? _registeredWait;
    private bool _disposed;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName,
            out _);

        IsPrimary = createdNew;

        if (IsPrimary)
        {
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _activationEvent,
                (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty),
                state: null,
                Timeout.InfiniteTimeSpan,
                executeOnlyOnce: false);
        }
        else
        {
            _activationEvent.Set();
        }
    }

    public bool IsPrimary { get; }

    public event EventHandler? ActivationRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registeredWait?.Unregister(null);
        _activationEvent.Dispose();

        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is exiting and the mutex is no longer owned.
            }
        }

        _mutex.Dispose();
    }
}
