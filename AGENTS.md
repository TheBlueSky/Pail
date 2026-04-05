# Pail - S3 Browser for Windows

A Windows desktop application for browsing and managing Amazon S3 buckets and objects.

## Project Structure

```
src/
├── S3Browser.Core/         # Business logic, models, services
└── S3Browser.App/          # WinUI 3 UI layer

test/
└── S3Browser.Tests.Unit/   # xUnit unit tests
```

## Tech Stack

- **.NET 8.0** (Windows)
- **WinUI 3** (Windows App SDK 1.8) - UI framework
- **CommunityToolkit.Mvvm** - MVVM implementation
- **CommunityToolkit.WinUI.UI.Controls.DataGrid** - Data grid component
- **AWS SDK for .NET** (AWSSDK.S3 4.0.19.1) - S3 API

## Architecture

- **MVVM pattern** with `CommunityToolkit.Mvvm`
- ViewModels use `[ObservableObject]` and `[RelayCommand]` attributes
- Interface-based services for testability and DI
- Centralized NuGet versioning via `Directory.Packages.props`

## Key Conventions

- Interfaces defined alongside implementations (e.g., `IS3Service` next to `S3Service`)
- Services injected via `Microsoft.Extensions.DependencyInjection`
- Downloads saved to `~/Downloads/Pail/` directory

## Build & Test Commands

```bash
dotnet build
dotnet test
```

## Supported Platforms

- x86, x64, ARM64

## AWS Authentication

Supports:
- Access Key / Secret Key
- Session Token (temporary credentials)
- Default Credential Chain (environment, IAM roles, etc.)
