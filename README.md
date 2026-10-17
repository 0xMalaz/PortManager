<img src="assets/icon/PortManager-256.png" alt="" width="96" align="right">

# Port Manager

[![Latest release](https://img.shields.io/github/v/release/0xMalaz/PortManager)](https://github.com/0xMalaz/PortManager/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows%2011%20x64-0078D6)

A small Windows tray utility for finding and freeing TCP ports used by local development servers.

"Port 3000 is already in use" — Port Manager shows you which process owns it and lets you stop it in two clicks, without digging through `netstat` and Task Manager.

## Features

- **Lives in the notification area** instead of occupying the taskbar. Left-click the tray icon to open it.
- **Shows every TCP listener** (IPv4 and IPv6), grouped by process and port.
- **Recognises dev servers.** Listeners are classified as **Dev** or **Other**, with common frameworks and services (Vite, Next.js, Node, Django, Postgres, Redis, …) identified from process names, command lines, executable paths, nearby project manifests and well-known ports. It never connects to your services.
- **Last activity** — shows when a TCP connection to each port was last observed, falling back to process start or first-seen time.
- **Search** by port, service, framework, PID, process name or bound address.
- **Safe termination.** Windows system processes are read-only, and every kill is confirmed with the full list of ports that will be affected (details in [Safety model](#safety-model)).
- **Light on resources.** Refreshes every two seconds while open and every five seconds while hidden, and runs in Windows efficiency mode while it sits in the tray.
- Follows the Windows light/dark theme, optional **Start with Windows**, and restarts elevated (UAC) when Windows denies a termination.

UDP listeners, Docker- and WSL-specific controls, code signing and unattended updates are currently out of scope.

## Install

1. Download `PortManager-Setup.exe` from the [latest release](https://github.com/0xMalaz/PortManager/releases/latest).
2. Run the installer. It installs for your Windows user; no .NET runtime is needed.
3. Open Port Manager from the Start menu.
4. Optionally right-click the tray icon and enable **Start with Windows**.

> **SmartScreen warning:** the executable is not code-signed, so Windows may show "Windows protected your PC" on first launch. Choose **More info → Run anyway**, or build it yourself from source (below).

If the icon is hidden under the `^` overflow menu, drag it into the visible area of the taskbar, or turn it on in **Settings → Personalization → Taskbar → Other system tray icons**.

To uninstall, choose **Exit**, then remove Port Manager in Windows **Installed apps**. The startup registration is removed, but your preference is retained in `HKCU\Software\0xMalaz\PortManager` and restored on reinstall. Updates also preserve it. Search text, filters and listener history remain session-only.

Existing portable users: exit the old copy, install the new release once, then delete the old executable. Launching the installed app migrates an enabled startup entry. Old portable executables cannot acquire an installer through an in-app update.

### Updates

Right-click the tray icon and choose **Check for updates**. A window shows the check result and download progress. When a newer version finishes downloading, Port Manager closes gracefully, applies it and relaunches automatically. Repeated clicks cannot start overlapping operations. A previously downloaded update is applied without another download; failures leave the app open and can be retried through the same action.

There are no background update checks, automatic downloads or startup update application. Unpackaged/development builds explain that an installed copy is required. Closing the progress window hides it; it does not cancel the update. **Exit** cancels outstanding work before an update has been handed to the installer.

See [release and local update testing](docs/updates.md) for packaging, settings and verification details.

## Usage

- **Left-click** the tray icon to toggle the popup. Click elsewhere or press **Esc** to hide it again.
- **Right-click** the tray icon for **Open**, **Refresh**, **Check for updates**, **Start with Windows** and **Exit**.
- Use the **All / Dev / Other** filters and the search box to find a port, then press **×** on its row and confirm.

Closing the popup only hides it. Use **Exit** from the tray menu to quit Port Manager.

## Safety model

- PID 0, PID 4, Port Manager itself, known critical Windows processes and executables under the Windows directory are visible but read-only.
- Port Manager normally runs without administrator privileges.
- A kill action always shows the owning process, PID and every listener that will be affected.
- The PID, process creation time and selected listener are checked again immediately before termination.
- The process tree is forcibly terminated, so unsaved work in that process can be lost.
- Success is only reported once the port is no longer listening. If a watcher immediately reopens the port, Port Manager reports the new owner instead.

Last-active tracking is polling-based, so connections shorter than the refresh interval may not be observed.

## Build from source

Requirements:

- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.302 or a newer .NET 10 feature band, see `global.json`)

```powershell
git clone https://github.com/0xMalaz/PortManager.git
cd PortManager
dotnet build .\PortManager.sln -c Debug
dotnet run --project .\src\PortManager.App\PortManager.App.csproj
```

The first manual launch opens the popup. Launching with `--startup` (as Windows startup does) starts hidden in the tray.

### Tests

```powershell
dotnet test .\PortManager.sln -c Debug
```

The suite includes a Windows integration test that launches a real process with two TCP listeners, discovers both through the native IP Helper table, terminates the owner, and verifies that both ports can be bound again.

### Publish a portable executable

```powershell
dotnet publish .\src\PortManager.App\PortManager.App.csproj -c Release -p:PublishProfile=win-x64
```

The self-contained single-file executable is written to `artifacts\publish\win-x64\PortManager.exe`.

Move the executable to a stable folder before enabling **Start with Windows**. If it is moved later, launching it once manually updates the startup entry to the new path.

## Project layout

```text
src/PortManager.App/        WPF tray application
  Native/                   P/Invoke: TCP table, process queries, power throttling
  Services/                 Snapshot provider, classifier, termination, tray, theme, startup
  ViewModels/  Views/       Popup window (MVVM)
tests/PortManager.Tests/    MSTest unit and integration tests
tests/PortManager.TestHost/ Helper process that opens TCP listeners for integration tests
assets/icon/                Icon source art (SVG masters, per-size PNGs, social preview)
scripts/                    Icon packing, release build and publishing
```

## Contributing

Bug reports, ideas and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## License

Port Manager is released under the [MIT License](LICENSE).
