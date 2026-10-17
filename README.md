<p align="center">
  <img src="assets/icon/PortManager-256.png" alt="Port Manager icon" width="96">
</p>

<h1 align="center">Port Manager</h1>

<p align="center">
  <a href="https://github.com/0xMalaz/PortManager/releases/latest"><img src="https://img.shields.io/github/v/release/0xMalaz/PortManager" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2011%20x64-0078D6" alt="Platform: Windows 11 x64">
</p>

A Windows tray app that shows which process is using a TCP port and lets you stop it.

## Features

- Lists every TCP listener (IPv4 and IPv6) with its process, PID and port.
- Tags dev servers like Vite, Next.js, Node, Django, Postgres and Redis.
- Search by port, process name, PID or service.
- System processes are read-only, and every kill asks for confirmation first.
- Follows the Windows light/dark theme.
- English and Turkish, picked from the Windows display language.
- Optional Start with Windows.

## Install

Download `PortManager-Setup.exe` from the [latest release](https://github.com/0xMalaz/PortManager/releases/latest) and run it. No .NET runtime is needed.

The app is not code-signed, so SmartScreen may warn you on first run. Click **More info** and then **Run anyway**.

Port Manager checks for new releases at startup, every 8 hours, and when you open the tray menu (at most once every 15 minutes). When one is available, the tray menu shows **Install update**. You can also choose **Check for updates** at any time.

## Usage

- Left-click the tray icon to open or close the window.
- Right-click it for updates, Start with Windows and Exit.
- Find a port, click **×** on its row and confirm.

Closing the window only hides it. Use **Exit** to quit.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build .\PortManager.sln
dotnet run --project .\src\PortManager.App\PortManager.App.csproj
dotnet test .\PortManager.sln
```

Release and update details are in [docs/updates.md](docs/updates.md).

## License

[MIT](LICENSE)
