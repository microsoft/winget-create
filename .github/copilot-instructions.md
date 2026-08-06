# WinGet Create (WingetCreate) Development Guide

## Project Overview

This repository contains the source for the **Windows Package Manager Manifest Creator** (`wingetcreate`), a .NET CLI tool that helps developers create, update, and submit package manifests to the [winget-pkgs](https://github.com/microsoft/winget-pkgs) community repository. The codebase is organized as:

- **`src/WingetCreateCLI`** - Command-line interface, argument parsing, and command implementations (New, Update, Submit, Show, Token, Settings, Cache, Info, Dsc)
- **`src/WingetCreateCore`** - Core manifest generation/parsing logic, installer detection, GitHub submission client
- **`src/WingetCreatePackage`** - MSIX packaging project for distribution
- **`src/WingetCreateTests`** - Unit and E2E tests

## Building, Testing, and Running

### Initial Setup

Configure your system using `.config/configuration.winget` (`winget configure .config/configuration.winget`), or manually install:
- Windows 10 1709 (16299) or later
- Visual Studio 2022 with **.NET Desktop Development** and **Universal Windows Platform Development** workloads
- Windows 11 SDK (10.0.26100.0)
- [Git LFS](https://git-lfs.github.com/)

### Building

Open `src\WingetCreateCLI.sln` in Visual Studio and build. Command-line `msbuild`/`dotnet build` also work.

### Testing

- Unit and E2E tests run via Visual Studio Test Explorer (`WingetCreateTests` project).
- Requires a fork of [winget-pkgs-submission-test](https://github.com/microsoft/winget-pkgs-submission-test) and a configured `WingetCreateTests/Test.runsettings` file (`WingetPkgsTestRepoOwner`, `WingetPkgsTestRepo`).
- A GitHub token is required for submission tests — prefer `wingetcreate token -s` over a raw PAT env var.

## Architecture & Key Patterns

- **Command pattern**: Each CLI verb (`New`, `Update`, `Submit`, etc.) is its own command class in `WingetCreateCLI/Commands`.
- **Manifest model**: Manifests are strongly-typed models generated from the [winget-pkgs manifest schemas](https://github.com/microsoft/winget-pkgs/tree/master/schemas/JSON/manifests), parsed/serialized as YAML.
- **Installer detection**: Core logic in `WingetCreateCore` downloads installers, computes hashes, and infers installer metadata (type, architecture, scope) automatically where possible.
- **GitHub submission flow**: `Submit`/`New`/`Update` commands can open PRs directly against `winget-pkgs` using a cached or provided GitHub PAT.
- **DSC v3 support**: The `Dsc` command surface implements DSC v3 resource commands for use with WinGet Configuration.

## Naming Conventions

- C# code follows StyleCop rules defined in `stylecop.json` at the repo root.
- Namespaces follow `Microsoft.WingetCreate.<Area>` (e.g., `Microsoft.WingetCreate.Core`, `Microsoft.WingetCreate.CLI.Commands`).

## Contributing

- Review `CONTRIBUTING.md` for the workflow and CLA requirements.
- Manifest schema compatibility with `winget-pkgs` is critical — verify against the current schema version (see `microsoft/winget-pkgs/schemas/JSON/manifests`) when changing manifest generation/parsing logic.
- CI runs on Azure Pipelines (`pipelines/`).
- Documentation for each command lives under `doc/` (e.g., `doc/new.md`, `doc/update.md`) — update alongside CLI changes.

## Telemetry & Privacy

The built/released `wingetcreate.exe` collects usage/diagnostic telemetry (respecting machine-wide privacy settings and `settings.json`'s `telemetry.disabled`). Locally-built binaries do not have telemetry enabled. See `PRIVACY.md` for details.
