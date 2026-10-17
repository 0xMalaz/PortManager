# Installation and manual updates

## Fit and architecture

PortManager is a Windows x64 .NET 10 WPF application with a WinForms tray icon and an explicit WPF shutdown lifecycle. It previously shipped as a self-contained portable executable with manual releases. Velopack supports this framework/platform and provides the installer, full/delta packages, GitHub feed and restart helper needed here. There is no framework blocker. Old portable users must install the first packaged release once. The GitHub repository is public, so the app needs no publishing or download credentials.

`Program.Main` runs Velopack before constructing WPF or acquiring the single-instance mutex. `SetAutoApplyOnStartup(false)` prevents a staged update from applying implicitly. The app checks the feed quietly at startup and every 8 hours (`CheckInBackgroundAsync`). A found release only relabels the tray item to "Install update (X.Y.Z)"; failures are silent. Downloading and applying still happen only when the tray action is invoked. `IAppUpdater` isolates library calls; `UpdateCoordinator` handles serialization, cancellation, errors and the shutdown handoff; `UpdateWindow` renders status/progress. A pending download skips the network check.

`WaitExitThenApplyUpdates` starts the helper waiting for process exit. The coordinator then requests WPF shutdown. `OnExit` cancels update work, stops the refresh timer, closes windows, disposes the tray/theme services and releases the single-instance handles before the process exits. Preferences are saved synchronously when changed, so there is no deferred settings save. The helper applies and restarts with no old command-line arguments (especially no stale elevation/wait PID arguments). The popup opens on restart. Velopack's helper waits up to 60 seconds; keep shutdown prompt and avoid blocking dialogs in `OnExit`.

Permanent package ID: **`0xMalaz.PortManager`**. Do not rename it after publishing. Default installation is under `%LOCALAPPDATA%\0xMalaz.PortManager`. The Start with Windows preference is stored separately in `HKCU\Software\0xMalaz\PortManager`. The uninstall hook removes only the Run registration, preserving this preference across reinstall. Future durable preferences must also live outside the install tree. Search/filter/history are not persisted today.

The SDK and local `vpk` tool are both pinned to **1.2.161**. Both versions were verified against the NuGet feeds, and the installed CLI help was checked. Update both pins together. Packaging checks that the local-feed compilation symbol matches the requested mode; CI also verifies default, enabled and disabled Release modes with `scripts/Test-UpdateBuildModes.ps1`. Relevant upstream documentation:

- [WPF bootstrap](https://docs.velopack.io/getting-started/wpf)
- [Manual integration, sources and startup behavior](https://docs.velopack.io/integrating/overview)
- [Graceful exit API](https://docs.velopack.io/reference/cs/Velopack/UpdateManager)
- [Packaging and delta assets](https://docs.velopack.io/packaging/overview)

## Release procedure

1. Bump the project `Version` to a new stable `X.Y.Z`, commit and push its matching `vX.Y.Z` tag. Existing published version tags must not be reused (including the portable 1.1.0 release).
2. The Windows workflow validates the exact tag/project version, runs tests, retrieves the newest older compatible full package, and builds the installer and feed. A legacy release without a Velopack full package is not a delta baseline. Network/authentication failures are fatal, not treated as first-release conditions.
3. Packaging uses `scripts/Build-Release.ps1`. Its output directory must be empty to exclude stale assets. It validates the SDK/CLI pins and project version. `-PreviousPackage` seeds delta generation from an older `0xMalaz.PortManager-X.Y.Z-full.nupkg`. Without it, packaging produces a full release.
4. `scripts/Publish-Release.ps1` refuses published releases, deletes/recreates only a verified failed draft (retaining the tag), and uploads the entire generated feed, full/delta packages, installer, portable archive and manifests. `PortManager-Setup.exe` is the stable installer alias. Every upload must succeed before publishing. No `--clobber` is used. All release jobs share a concurrency group. Do not manually publish or mutate a draft while automation owns it; GitHub does not provide an atomic draft-state-conditioned asset upload/delete API.
5. On failure, inspect the draft and rerun the workflow. If publication succeeded but the final response was lost, the rerun refuses the now-published release. Fix later problems with a new version. Only CI gets `GITHUB_TOKEN` with contents-write permission; the app contains no credentials.

Local production packaging (matching project version):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 1.2.0 -OutputDir artifacts/my-release
```

Code signing is not configured. As with previous releases, Windows may show SmartScreen warnings. Configure signing before wider distribution if required; it does not block the update architecture.

## Local two-version installation/update/restart test

Use a disposable Windows user or VM: these packages use the real permanent application ID and must not replace your normal installation. Never publish local-feed test artifacts. The production Release build ignores `PORTMANAGER_UPDATE_SOURCE`; only Debug or explicitly opted-in `-LocalFeedTest` packages read it. A source override does not make an unpackaged executable updateable.

```powershell
# Use two fresh output directories and versions higher than anything installed in the VM.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 9.0.0 -LocalFeedTest -OutputDir artifacts/test-v1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 9.0.1 -LocalFeedTest -OutputDir artifacts/test-v2 -PreviousPackage artifacts/test-v1/0xMalaz.PortManager-9.0.0-full.nupkg
$env:PORTMANAGER_UPDATE_SOURCE = (Resolve-Path artifacts/test-v2).Path
# Launch the installer from this shell so the first app launch inherits the override.
& ./artifacts/test-v1/PortManager-Setup.exe
```

1. Enable Start with Windows in v1. Verify the Run value and retained preference, and note the app PID and executable file version. Leave it idle and restart once: neither action should download or apply an update, but the tray item should read "Install update (9.0.1)" once the startup check finds v2.
2. Choose Check for updates. Observe progress, click it repeatedly, and verify one operation. The old process must exit, its tray icon disappear and a single new process launch with file version 9.0.1. Verify the popup opens and startup preference/registration survive. Check again: it should report no update.
3. Repeat from a clean v1 install with an unreachable source or disconnected network. Verify an actionable failure and a retry that succeeds after restoring connectivity. For interrupted download behavior, exit while downloading and verify no apply/restart occurs.
4. To test a pending download deterministically, use a temporary local test modification which returns just before `ApplyAfterExit`, rebuild v1, download v2, then exit. On next launch, verify no automatic application. The explicit update action should apply the staged package without another network request (disconnect the feed). Do not commit or publish this temporary test modification.
5. Uninstall with Windows Installed apps, verify the Run entry is removed and the preference key remains. Reinstall and verify startup preference restoration. Repeat with startup disabled to verify it stays disabled. Exit elevated mode before this test; separately exercise an elevated update to check UAC/relaunch behavior in your deployment environment.
6. Repeat using a local HTTP feed if desired (`python -m http.server 8765 --directory artifacts/test-v2`, source `http://127.0.0.1:8765`). Serve the entire directory, including the index and packages.

## Verification boundaries

Fake-updater tests exercise explicit-only download/apply, quiet background checks, unavailable/no-update states, check/download/apply failures, pending-download reuse, operation ordering, repeated clicks and cancellation during a non-cancelable check. They do not prove installer behavior, registry persistence on uninstall, tray cleanup or real restart. Local packaging validates the actual SDK/CLI and produced artifacts. The two-version installed/UI path, GitHub publication and hosted CI must be recorded separately when exercised; building packages or passing unit tests is not an end-to-end update test.

Implementation verification: 81 Release tests passed, including isolated registry preference migration and uninstall/reinstall simulation. Four offline publishing checks passed (published-release protection, query failure, partial upload failure, draft retry). Local full/installer packaging and a 1.1.0-to-1.2.0 delta build succeeded with CLI 1.2.161, which verified the bootstrap call. These packaging checks used local builds, not the previously published portable release. Real installed update/relaunch, actual Windows uninstall, elevated updates, hosted CI and GitHub publication have not been exercised. No release was published during implementation.
