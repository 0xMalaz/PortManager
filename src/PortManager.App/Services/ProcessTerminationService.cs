using System.ComponentModel;
using System.Diagnostics;
using PortManager.Models;

namespace PortManager.Services;

public sealed class ProcessTerminationService : IProcessTerminationService
{
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(3);
    private readonly IPortSnapshotProvider _snapshotProvider;
    private readonly Func<PortListener, CancellationToken, Task> _terminateProcess;
    private readonly TimeSpan _portReleaseWait;
    private readonly TimeSpan _pollInterval;

    public ProcessTerminationService(IPortSnapshotProvider snapshotProvider)
        : this(
            snapshotProvider,
            TerminateProcessTreeAsync,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(150))
    {
    }

    internal ProcessTerminationService(
        IPortSnapshotProvider snapshotProvider,
        Func<PortListener, CancellationToken, Task> terminateProcess,
        TimeSpan portReleaseWait,
        TimeSpan pollInterval)
    {
        _snapshotProvider = snapshotProvider;
        _terminateProcess = terminateProcess;
        _portReleaseWait = portReleaseWait;
        _pollInterval = pollInterval;
    }

    public async Task<TerminationPreview> PrepareAsync(
        PortListener listener,
        CancellationToken cancellationToken = default)
    {
        if (listener.IsProtected)
        {
            throw new ProtectedProcessException(listener.ProtectionReason ?? "This process is protected.");
        }

        var snapshot = await _snapshotProvider.GetListenersAsync(cancellationToken);
        var current = FindMatchingListener(snapshot, listener);

        if (current is null)
        {
            throw new ListenerStaleException($"Port {listener.Port} is no longer owned by that process.");
        }

        if (current.IsProtected)
        {
            throw new ProtectedProcessException(current.ProtectionReason ?? "This process is protected.");
        }

        var ownedPorts = snapshot
            .Where(item => item.ProcessId == current.ProcessId && IdentityMatches(current, item))
            .Select(item => item.Port)
            .Distinct()
            .OrderBy(port => port)
            .ToArray();

        return new TerminationPreview(current, ownedPorts);
    }

    public async Task<TerminationResult> TerminateAsync(
        TerminationPreview preview,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _snapshotProvider.GetListenersAsync(cancellationToken);
            var current = FindMatchingListener(snapshot, preview.Listener);

            if (current is null)
            {
                return new TerminationResult(
                    TerminationOutcome.Stale,
                    $"Port {preview.Listener.Port} changed before it could be terminated.");
            }

            if (current.IsProtected)
            {
                return new TerminationResult(
                    TerminationOutcome.Protected,
                    current.ProtectionReason ?? "This process is protected.",
                    current);
            }

            await _terminateProcess(current, cancellationToken);

            return await WaitForPortReleaseAsync(current.Port, cancellationToken);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            return new TerminationResult(
                TerminationOutcome.AccessDenied,
                "Windows denied permission to terminate this process.");
        }
        catch (ArgumentException)
        {
            return await ResultAfterProcessDisappearedAsync(preview.Listener.Port, cancellationToken);
        }
        catch (ListenerStaleException exception)
        {
            return new TerminationResult(TerminationOutcome.Stale, exception.Message);
        }
        catch (InvalidOperationException)
        {
            return await ResultAfterProcessDisappearedAsync(preview.Listener.Port, cancellationToken);
        }
        catch (AggregateException exception)
        {
            return new TerminationResult(
                TerminationOutcome.Failed,
                $"The process tree could not be fully terminated: {exception.GetBaseException().Message}");
        }
        catch (Win32Exception exception)
        {
            return new TerminationResult(
                TerminationOutcome.Failed,
                $"Windows could not terminate the process: {exception.Message}");
        }
    }

    internal static bool IdentityMatches(PortListener expected, PortListener current)
    {
        if (expected.ProcessId != current.ProcessId)
        {
            return false;
        }

        if (expected.ProcessStartTimeUtc.HasValue || current.ProcessStartTimeUtc.HasValue)
        {
            return expected.ProcessStartTimeUtc == current.ProcessStartTimeUtc;
        }

        return string.Equals(expected.ProcessName, current.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    private static PortListener? FindMatchingListener(
        IReadOnlyList<PortListener> snapshot,
        PortListener expected) =>
        snapshot.FirstOrDefault(item =>
            item.Port == expected.Port &&
            IdentityMatches(expected, item));

    private static bool ProcessIdentityMatches(Process process, PortListener listener)
    {
        if (!listener.ProcessStartTimeUtc.HasValue)
        {
            return string.Equals(process.ProcessName, listener.ProcessName, StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var currentStartTime = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
            return currentStartTime == listener.ProcessStartTimeUtc;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static async Task TerminateProcessTreeAsync(
        PortListener listener,
        CancellationToken cancellationToken)
    {
        using var process = Process.GetProcessById(listener.ProcessId);
        if (!ProcessIdentityMatches(process, listener))
        {
            throw new ListenerStaleException("The process identity changed. The list has been refreshed.");
        }

        process.Kill(entireProcessTree: true);
        await WaitForExitWithoutBlockingAsync(process, cancellationToken);
    }

    private static async Task WaitForExitWithoutBlockingAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ExitWait);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The port verification below is the final success criterion.
        }
    }

    private async Task<TerminationResult> WaitForPortReleaseAsync(
        int port,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + _portReleaseWait;
        PortListener? currentOwner = null;

        do
        {
            var snapshot = await _snapshotProvider.GetListenersAsync(cancellationToken);
            currentOwner = snapshot.FirstOrDefault(listener => listener.Port == port);

            if (currentOwner is null)
            {
                return new TerminationResult(
                    TerminationOutcome.Success,
                    $"Port {port} is free.");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return new TerminationResult(
            TerminationOutcome.StillListening,
            $"Port {port} is still listening under {currentOwner?.ProcessName ?? "another process"}.",
            currentOwner);
    }

    private async Task<TerminationResult> ResultAfterProcessDisappearedAsync(
        int port,
        CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetListenersAsync(cancellationToken);
        var currentOwner = snapshot.FirstOrDefault(listener => listener.Port == port);

        return currentOwner is null
            ? new TerminationResult(TerminationOutcome.Success, $"Port {port} is free.")
            : new TerminationResult(
                TerminationOutcome.Stale,
                $"The original process exited, but port {port} now belongs to {currentOwner.ProcessName}.",
                currentOwner);
    }
}
