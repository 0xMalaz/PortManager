using Velopack;
using Velopack.Sources;

namespace PortManager.Services;

internal interface IAppUpdater
{
    bool CanUpdate { get; }
    string? PendingVersion { get; }
    Task<string?> CheckAsync(CancellationToken cancellationToken);
    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);
    void ApplyAfterExit();
}

internal sealed class VelopackAppUpdater : IAppUpdater
{
    private readonly UpdateManager _manager = CreateManager();
    private UpdateInfo? _available;

    public bool CanUpdate => _manager.IsInstalled;
    public string? PendingVersion => CanUpdate ? _manager.UpdatePendingRestart?.Version.ToString() : null;

    public async Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _available = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return _available?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken) =>
        _manager.DownloadUpdatesAsync(_available ?? throw new InvalidOperationException("No update selected."),
            progress.Report, cancellationToken);

    public void ApplyAfterExit() => _manager.WaitExitThenApplyUpdates(
        _manager.UpdatePendingRestart ?? throw new InvalidOperationException("No downloaded update is available."),
        silent: false, restart: true, restartArgs: []);

    private static UpdateManager CreateManager()
    {
#if DEBUG || LOCAL_UPDATE_FEED
        var source = Environment.GetEnvironmentVariable("PORTMANAGER_UPDATE_SOURCE");
        if (!string.IsNullOrWhiteSpace(source)) return new UpdateManager(source);
#endif
        return new UpdateManager(new GithubSource("https://github.com/0xMalaz/PortManager", null, false));
    }
}
