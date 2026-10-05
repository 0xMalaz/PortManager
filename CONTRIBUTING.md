# Contributing to Port Manager

Thanks for your interest in improving Port Manager! Bug reports, feature ideas and pull requests are all welcome.

## Reporting bugs and requesting features

Open an [issue](https://github.com/0xMalaz/PortManager/issues) and include:

- your Windows version and the Port Manager version (from the release you downloaded or the commit you built),
- what you did, what you expected and what happened instead,
- for misclassified listeners: the port, the process name and, if you are comfortable sharing it, the command line.

For larger changes, please open an issue first so we can agree on the approach before you invest time in a pull request.

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the version is pinned in `global.json`).
2. Clone the repository and build:

   ```powershell
   git clone https://github.com/0xMalaz/PortManager.git
   cd PortManager
   dotnet build .\PortManager.sln -c Debug
   dotnet test .\PortManager.sln -c Debug
   ```

3. Run the app with `dotnet run --project .\src\PortManager.App\PortManager.App.csproj`.

Only one instance can run at a time. To test a Debug build while your normal copy keeps running, copy the built `PortManager.exe` to `PortManager.UiTest.exe` and run that. In Debug builds this name uses a separate single-instance lock and keeps the popup open and visible in the taskbar.

## Making changes

- Keep pull requests focused on one change, and add or update tests in `tests/PortManager.Tests` for behaviour you change.
- Match the surrounding code style: nullable reference types are enabled, and file-scoped namespaces and descriptive names are used throughout.
- Native Windows calls live in `src/PortManager.App/Native`. Keep P/Invoke signatures there and expose small, testable helpers.
- Anything that can terminate a process must keep the guarantees in the README's [Safety model](README.md#safety-model).
- Port Manager runs in the tray all day. Avoid adding work to the background refresh path, and avoid timers or animations that run while the popup is hidden.
- Make sure `dotnet build` produces no new warnings and `dotnet test` passes before opening a pull request.

## Pull request titles

Pull request titles become the entries in the release notes, so please use a [Conventional Commits](https://www.conventionalcommits.org/) style prefix:

| Prefix | Use for |
|---|---|
| `feat:` | New user-visible functionality |
| `fix:` | Bug fixes |
| `perf:` | Performance or resource-usage improvements |
| `docs:` | Documentation only |
| `refactor:` | Code changes that don't change behaviour |
| `test:` | Tests only |
| `chore:` | Build, tooling and release housekeeping |

Example: `fix: keep the popup open while a confirmation dialog is shown`.

## Releases (maintainers)

1. Bump `<Version>` in `src/PortManager.App/PortManager.App.csproj`.
2. Publish: `dotnet publish .\src\PortManager.App\PortManager.App.csproj -c Release -p:PublishProfile=win-x64`.
3. Create a GitHub release tagged `vX.Y.Z` with generated release notes, and attach `artifacts\publish\win-x64\PortManager.exe`.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
