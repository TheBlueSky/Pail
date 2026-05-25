# Pail - S3 Browser for Windows

This file provides guidance to AI coding agents (Claude Code, GitHub Copilot, OpenCode, OpenAI Codex, etc.) when working with code in this repository.

## What this is

Pail is a Windows desktop app (WinUI 3, Windows App SDK 2.0.1, .NET 10) for browsing Amazon S3 buckets and downloading objects/folders. Solution file is `Pail.slnx` (the new XML solution format).

## Common commands

```powershell
dotnet build
dotnet test

# Run a single test project / class / method
dotnet test test/Pail.Core.Tests.Unit/Pail.Core.Tests.Unit.csproj
dotnet test --filter "FullyQualifiedName~DownloadManagerTests"
dotnet test --filter "FullyQualifiedName=Pail.Core.Tests.Unit.Services.S3ServiceTests.SomeMethod"

# Run the app from CLI (must specify a Windows RID — there is no AnyCPU)
dotnet run --project src/Pail.App -r win-x64

# Self-contained single-file publish (matches release artifacts)
dotnet publish src/Pail.App -c Release -r win-x64 --self-contained
```

Notes that bite:
- SDK is pinned by `global.json` to `10.0.203` (rollForward `latestFeature`). The .NET 10 SDK must be installed.
- `TreatWarningsAsErrors=true` is set globally in `Directory.Build.props` — warnings will fail the build.
- The app project targets `net10.0-windows10.0.19041.0` and only builds for `x86;x64;ARM64` (no AnyCPU). Publishing without an explicit RID will fail.
- `EnableWindowsTargeting=true` is set, so non-Windows machines can restore/build but obviously can't run the UI.

## Project Structure

```text
src/
├── Pail.Core/         # Business logic, models, services
└── Pail.App/          # WinUI 3 UI layer

test/
└── Pail.Core.Tests.Unit/   # xUnit unit tests
```

## Architecture (the parts you have to read multiple files to see)

Two-project split with a deliberate UI/logic boundary:

- **`src/Pail.Core`** — root namespace `Pail` (note: not `Pail.Core`). Contains models, services, and ViewModels. References `AWSSDK.S3`, `CommunityToolkit.Mvvm`, and `Microsoft.Extensions.Options`/`Configuration.Abstractions` only — no WinUI/WindowsAppSDK dependency. ViewModels live here so they're unit-testable. Has `InternalsVisibleTo Pail.Core.Tests.Unit`.
- **`src/Pail.App`** — root namespace `Pail.App`, assembly name `Pail`. WinUI 3 shell: `App.xaml(.cs)`, Views (`*Page.xaml`), Converters, Controls, and platform-specific service implementations (clipboard, folder picker, dispatcher, navigation host, theme application, localization via resource lookup).
- **`test/Pail.Core.Tests.Unit`** — xUnit + NSubstitute. Only tests `Pail.Core`; the WinUI layer is not unit-tested.

### DI / composition root

All wiring lives in `PailApp.ConfigureServices()` in `src/Pail.App/App.xaml.cs`. Services are singletons; ViewModels are transient. When adding a new service:
- Define the interface in `Pail.Core/Services` if it's pure logic, or in `Pail.App/Services` if it requires WinUI/WinRT types.
- Register it in `ConfigureServices`. The `INavigationService` (Core) and `INavigationHostService` (App) pattern — where the App-layer interface extends/implements the Core interface and both registrations resolve to the same instance — is intentional: it lets Core ViewModels navigate without taking a WinUI dependency. Follow the same pattern for any other service that needs both a Core-facing and App-facing surface.

### Settings

`appsettings.user.json` (next to the executable, see `SettingsService.DefaultFileName`) is loaded via `IConfiguration` with `reloadOnChange: true` and bound to `AppSettings` through `IOptionsMonitor<AppSettings>`. `SettingsService` reads via the monitor and writes by serializing back to the same file. Don't introduce a parallel settings store — extend `AppSettings` instead.

### Navigation and status messages

- `INavigationHostService` (implemented by `NavigationService`) wraps the root `Frame`; it's initialized in `OnLaunched` after the window is created and then drives all page navigation by string key (`"LoginPage"`, etc.).
- A `StatusOverlayHost` control is layered above the root `Frame` in a `Grid` at `ZIndex=10`. `IStatusMessageService` + `StatusInfoBarPresenter` push transient InfoBars into it — don't add page-level status UI; route through the service.

### Downloads

`IDownloadManager` (`DownloadManager`) coordinates downloads as a queue of `DownloadItem`s with `DownloadStatus` and `DownloadProgress` events. `S3Service` exposes progress + cancellation; the download path uses `StreamDownloadExtensions` and `SyncProgress` to throttle progress callbacks back onto the UI thread via `IDispatcherService`. When changing download behaviour, keep the cancellation/progress contracts on `IS3Service` intact — `DownloadManager` and the ViewModels both depend on them.

## Conventions enforced by tooling

- `.editorconfig` mandates **tabs (width 4) for `.cs` files**, spaces elsewhere, CRLF line endings, final newline. Don't reformat with spaces.
- Nullable reference types and implicit usings are on globally.
- `Pail.App/Imports.cs` defines global usings for `Microsoft.UI.Xaml`, `Microsoft.UI.Xaml.Controls`, and `Pail.App.Views` — don't re-import these in App-layer files.
- New NuGet packages go in `Directory.Packages.props` (central package management is on); reference them without a `Version` in the `.csproj`.
