namespace PortManager.Services;

internal enum UpdateStage { Idle, Available, Checking, Downloading, NoUpdate, Unavailable, Failed, Restarting }
internal sealed record UpdateStatus(UpdateStage Stage, string Message, int Progress = 0, string? Version = null);

/// <summary>Only RunAsync downloads or applies; CheckInBackgroundAsync only looks. UI subscribers run on the caller's synchronization context.</summary>
internal sealed class UpdateCoordinator(IAppUpdater updater, Action shutdown) : IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private int _running;
    private bool _disposed;
    private Task _backgroundCheck = Task.CompletedTask;
    public UpdateStatus Status { get; private set; } = new(UpdateStage.Idle, "Check for updates");
    public event Action<UpdateStatus>? StatusChanged;

    public async Task RunAsync()
    {
        // Let a poll that is already in flight finish so this click is not silently dropped.
        await _backgroundCheck;
        if (_disposed || Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        var restarting = false;
        var token = _lifetime.Token;
        try
        {
            if (!updater.CanUpdate)
            {
                Report(new(UpdateStage.Unavailable, "Updates are unavailable in development or unpackaged builds. Install PortManager-Setup.exe first."));
                return;
            }

            if (updater.PendingVersion is null)
            {
                Report(new(UpdateStage.Checking, "Checking for updates…"));
                var version = await updater.CheckAsync(token);
                token.ThrowIfCancellationRequested();
                if (version is null)
                {
                    Report(new(UpdateStage.NoUpdate, "Port Manager is up to date."));
                    return;
                }

                Report(new(UpdateStage.Downloading, $"Downloading {version}…"));
                var downloading = true;
                var progress = new Progress<int>(percent =>
                {
                    if (!_disposed && downloading && Status.Stage == UpdateStage.Downloading)
                        Report(new(UpdateStage.Downloading, $"Downloading {version}: {Math.Clamp(percent, 0, 100)}%", Math.Clamp(percent, 0, 100)));
                });
                try { await updater.DownloadAsync(progress, token); }
                finally { downloading = false; }
                token.ThrowIfCancellationRequested();
                Report(new(UpdateStage.Downloading, $"Downloaded {version}: 100%", 100));
            }

            token.ThrowIfCancellationRequested();
            updater.ApplyAfterExit();
            restarting = true;
            Report(new(UpdateStage.Restarting, "Restarting to apply the update…"));
            shutdown();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Report(new(UpdateStage.Failed, $"Update failed: {exception.Message}\nChoose Check for updates to retry."));
        }
        finally
        {
            if (!restarting) Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <summary>Silently looks for a newer release. Reports only when one is found; failures are ignored.</summary>
    public Task CheckInBackgroundAsync()
    {
        if (_disposed || !updater.CanUpdate || Interlocked.CompareExchange(ref _running, 1, 0) != 0) return Task.CompletedTask;
        return _backgroundCheck = CheckCoreAsync();
    }

    private async Task CheckCoreAsync()
    {
        var token = _lifetime.Token;
        try
        {
            var version = updater.PendingVersion ?? await updater.CheckAsync(token);
            token.ThrowIfCancellationRequested();
            if (version is not null)
                Report(new(UpdateStage.Available, $"Port Manager {version} is available.", Version: version));
        }
        catch (Exception) { /* Background checks stay quiet; the next poll or a manual check retries. */ }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void Report(UpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        // An outstanding check may still inspect its token after cancellation.
    }
}
