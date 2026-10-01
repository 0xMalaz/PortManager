# Port Manager

Port Manager is a small Windows 11 tray utility for finding and freeing TCP ports used by local development servers.

It reads the Windows TCP listener table directly, shows the process that owns each listening port, and can terminate that process and its descendants after an explicit confirmation.

## What it does

- Lives in the Windows notification area instead of occupying the taskbar.
- Shows IPv4 and IPv6 TCP listeners, grouped by process and port.
- Classifies listeners as **Dev** or **Other** and identifies common local services and frameworks from passive process and project metadata.
- Shows the last TCP connection activity observed while Port Manager is running, with process start or first-seen time as the fallback.
- Searches by port, service, framework, PID, process name, or bound address.
- Refreshes every two seconds while open and every five seconds while hidden.
- Protects Windows system processes from termination.
- Revalidates PID and process start time before killing anything.
- Confirms every termination and lists all ports owned by the affected process.
- Reports success only after the selected port is no longer listening.
- Supports an optional, per-user **Start with Windows** toggle.
- Can restart with UAC elevation when Windows denies process termination.

UDP listeners, Docker-specific controls, WSL-specific controls, signing, installation, and automatic updates are intentionally outside the MVP.

Framework detection does not contact local services. It uses process names, command lines, executable paths, nearby project manifests, and dedicated well-known service ports. Last-active tracking is polling-based, so connections shorter than the refresh interval may not be observed.

## Requirements

- Windows 11 x64 for the supported MVP target.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source.
- The published executable is self-contained and does not require a separately installed .NET runtime.

## Restore after reinstalling Windows

### Run the portable app

1. Sign in to GitHub and open the private [PortManager releases](https://github.com/0xMalaz/PortManager/releases).
2. Download `PortManager.exe` from a release and put it in a stable directory, such as `%LOCALAPPDATA%\Programs\PortManager`.
3. Launch `PortManager.exe`. No installer or .NET SDK/runtime is required.
4. Enable **Start with Windows** from the tray menu if desired. This per-user Windows setting must be enabled again after formatting.

### Restore the source and rebuild

Install Git and the .NET 10 SDK (10.0.302 or a newer stable .NET 10 SDK, as required by `global.json`), then run:

```powershell
git clone https://github.com/0xMalaz/PortManager.git
Set-Location PortManager
dotnet restore .\PortManager.sln
dotnet test .\PortManager.sln -c Debug
dotnet publish .\src\PortManager.App\PortManager.App.csproj -c Release -p:PublishProfile=win-x64
```

GitHub requires access to the private repository; authenticate with Git Credential Manager when cloning. The rebuilt executable is `artifacts\publish\win-x64\PortManager.exe`.

Git preserves the source, tests, icon, and build/publish configuration. Generated `bin`, `obj`, and `artifacts` directories are excluded and can be rebuilt. Portable executables are saved separately as GitHub release assets. The app has no project-specific settings file or database to restore.

## Build and run

```powershell
dotnet build .\PortManager.sln -c Debug
dotnet run --project .\src\PortManager.App\PortManager.App.csproj
```

The first manual launch opens the popup. Closing the popup only hides it; use **Exit** from the tray icon's context menu to stop Port Manager.

Windows controls whether notification-area icons are directly visible or placed under the `^` overflow menu. You can drag Port Manager into the visible area or change its Windows taskbar notification settings.

## Tests

```powershell
dotnet test .\PortManager.sln -c Debug
```

The suite includes a Windows integration test that launches a real process with two TCP listeners, discovers both through the native IP Helper table, terminates the owner, and verifies that both ports can be rebound.

## Publish

```powershell
dotnet publish .\src\PortManager.App\PortManager.App.csproj `
  -c Release `
  -p:PublishProfile=win-x64
```

The portable executable is written to:

```text
artifacts\publish\win-x64\PortManager.exe
```

Move the executable to a stable directory before enabling **Start with Windows**. If it is moved later, manually launching it once updates an existing startup entry to the new path.

## Safety model

- PID 0, PID 4, Port Manager itself, known critical Windows processes, and executables under the Windows directory are visible but read-only.
- Port Manager normally runs without administrator privileges.
- A kill action always shows the owning process, PID, and every listener that will be affected.
- The PID, process creation time, and selected listener are checked again immediately before termination.
- The process tree is forcibly terminated, so unsaved work in that process can be lost.
- If a watcher immediately reopens a port, Port Manager reports the new owner instead of claiming success.
