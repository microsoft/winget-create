
# Welcome to the Windows Package Manager Manifest Creator repository.

This repository contains the source code for the Windows Package Manager Manifest Creator.  The  Windows Package Manager Manifest Creator is designed to help generate or update manifest files for the [Community repo](https://github.com/microsoft/winget-pkgs).

## Overview

**Windows Package Manager Manifest Creator** is an Open Source tool designed to help developers create, update, and submit manifest files to the [Windows Package Manager repository](https://github.com/microsoft/winget-pkgs).

Developers will use this tool to submit their applications for use with the [Windows Package Manager](https://docs.microsoft.com/windows/package-manager/).

## Getting Started

For your convenience, **WingetCreate** can be acquired a number of ways.

### Install from the github repo

The **Windows Package Manager Manifest Creator** is available for download from the [winget-create](https://github.com/microsoft/winget-create/releases) repository.  To install the package, simply click the the MSIX file in your browser.  Once it has downloaded, click open.

### Install with Windows Package Manager

```powershell
winget install wingetcreate
```

### Install with [Scoop](https://scoop.sh/)

```powershell
scoop install wingetcreate
```

### Install with [Chocolatey](https://chocolatey.org/)

```powershell
choco install wingetcreate
```

## Build status

[![Build Status](https://microsoft.visualstudio.com/Apps/_apis/build/status%2FADEX%2Fwinget-create%20Release?repoName=microsoft%2Fwinget-create&branchName=main)](https://microsoft.visualstudio.com/Apps/_build/latest?definitionId=64953&repoName=microsoft%2Fwinget-create&branchName=main)
## Using Windows Package Manager Manifest Creator

**WingetCreate** has the following commands:

| Command  | Description |
| -------  | ----------- |
| [New](doc/new.md)      | Command for creating a new manifest from scratch |
| [Update](doc/update.md)  | Command for updating an existing manifest |
| [New-Locale](doc/new-locale.md)  | Command for creating a new locale for an existing manifest |
| [Update-Locale](doc/update-locale.md)  | Command for updating a locale for an existing manifest |
| [Submit](doc/submit.md)  | Command for submitting an existing PR  |
| [Show](doc/show.md)      | Command for displaying existing manifests  |
| [Token](doc/token.md)   | Command for managing cached GitHub personal access tokens |
| [Settings](doc/settings.md) | Command for editing the settings file configurations |
| [Cache](doc/cache.md) | Command for managing downloaded installers stored in cache
| [Info](doc/info.md)      | Displays information about the client |
| [Dsc](doc/dsc.md)      | DSC v3 resource commands |
| [-?](doc/help.md)      | Displays command line help |

Click on the individual commands to learn more.

## Using Windows Package Manager Manifest Creator in a CI/CD pipeline

You can use WingetCreate to update your existing app manifest as part of your CI/CD pipeline. For reference, see the final task in this repo's [release Azure pipeline](https://github.com/microsoft/winget-create/blob/main/pipelines/azure-pipelines.release.yml). If you are utilizing GitHub Actions as your CI pipeline, you can refer to the following repositories that have implemented WingetCreate within their release pipelines:

- [Copilot CLI](https://github.com/github/copilot-cli/blob/v0.0.368-2/.github/workflows/winget.yml)
- [Edit](https://github.com/microsoft/edit/blob/main/.github/workflows/winget.yml)
- [Oh-My-Posh](https://github.com/JanDeDobbeleer/oh-my-posh/blob/main/.github/workflows/release.yml#L139)
- [PowerToys](https://github.com/microsoft/PowerToys/blob/main/.github/workflows/package-submissions.yml)
- [Terminal](https://github.com/microsoft/terminal/blob/main/.github/workflows/winget.yml)
- [WinGet Studio](https://github.com/microsoft/winget-studio/blob/main/.github/workflows/winget.yml)

You can also check out this [episode of Open at Microsoft](https://learn.microsoft.com/en-us/shows/open-at-microsoft/wingetcreate-keeping-winget-packages-up-to-date) where we cover the same topic.

### Using the standalone exe:

The latest version of the standalone exe can be found at https://aka.ms/wingetcreate/latest, and the latest preview version can be found at https://aka.ms/wingetcreate/preview, both of these require [.NET Runtime 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) to be installed on the build machine. To install this on your build machine in your pipeline, you can include the following dotnet task:

```yaml
      - task: UseDotNet@2
        displayName: 'Install .NET Runtime'
        inputs:
          packageType: sdk
          version: '6.x'
          installationPath: '$(ProgramFiles)\dotnet'
```

Or you can utilize a PowerShell task and run the following script.

```PowerShell
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
    .\dotnet-install.ps1 -Runtime dotnet -Architecture x64 -Version 6.0.13 -InstallDir $env:ProgramFiles\dotnet
```

> [!IMPORTANT]
> Make sure your build machine has the [Microsoft Visual C++ Redistributable for Visual Studio](https://support.microsoft.com/en-us/topic/the-latest-supported-visual-c-downloads-2647da03-1eea-4433-9aff-95f26a218cc0) already installed. Without this, the standalone WingetCreate exe will fail to execute and likely show a "DllNotFoundException" error.

To execute the standalone exe, add another PowerShell task to download and run the ./wingetcreate.exe to update your existing manifest. You will need a GitHub personal access token if you would like to submit your updated manifest. It is not recommended to hardcode your PAT in your script as this poses as a security threat. You should instead store your PAT as a [secret pipeline variable](https://docs.microsoft.com/azure/devops/pipelines/process/variables?view=azure-devops&tabs=yaml%2Cbatch#secret-variables) or a [repository secret](https://docs.github.com/en/actions/security-guides/encrypted-secrets#creating-encrypted-secrets-for-a-repository) in case of GitHub Actions.

```PowerShell
    Invoke-WebRequest https://aka.ms/wingetcreate/latest -OutFile wingetcreate.exe
    .\wingetcreate.exe update <packageId> -u $(packageUrls) -v $(manifestVersion) -t $(GITHUB_PAT)
```

### Using the msixbundle:

Windows Server 2022 now supports App Execution Aliases, which means the alias `wingetcreate` can be used to run the tool after installing the msixbundle. The latest version of the msixbundle can be found at https://aka.ms/wingetcreate/latest/msixbundle. Similar to the standalone exe steps, download the msixbundle, add the package, and run `wingetcreate` to update your manifest.

> [!IMPORTANT]
> Winget-Create has a dependency on the [C++ Runtime Desktop framework package](https://docs.microsoft.com/en-us/troubleshoot/developer/visualstudio/cpp/libraries/c-runtime-packages-desktop-bridge). Be sure to also download and install this package prior to installing wingetcreate as shown in the steps below.

```yaml
- powershell: |
        # Download and install C++ Runtime framework package.
        iwr https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx -OutFile $(vcLibsBundleFile)
        Add-AppxPackage $(vcLibsBundleFile)

        # Download Winget-Create msixbundle, install, and execute update.
        iwr https://aka.ms/wingetcreate/latest/msixbundle -OutFile $(appxBundleFile)
        Add-AppxPackage $(appxBundleFile)
        wingetcreate update Microsoft.WingetCreate -u $(packageUrl) -v $(manifestVersion) -t $(GITHUB_PAT) --submit
```

The CLI also supports creating or updating manifests with multiple installer URLs. You can either create new manifests with multiple installer nodes using the [New Command](doc/new.md) or update existing manifests with multiple installer URLs using the [Update Command](doc/update.md).

## GitHub Personal Access Token (classic) Permissions

When [creating your own GitHub Personal Access Token (PAT)](https://docs.github.com/en/github/authenticating-to-github/keeping-your-account-and-data-secure/creating-a-personal-access-token) to be used with WingetCreate, make sure the following permissions are selected.

- Select the **public_repo** scope to allow access to public repositories

![public_repo scope](./doc/images/tokenscope-publicrepo.png)

- (Optional) Select the **delete_repo** scope permission if you want WingetCreate to automatically delete the forked repo that it created if the PR submission fails.

## Building the client

### Prerequisites

You can install the prerequisites in one of two ways:

#### Using the configuration file

1. Clone the repository
2. Configure your system
   * Configure your system using the [configuration file](.config/configuration.winget). To run the configuration, use `winget configure .config/configuration.winget` from the project root or you can double-click the file directly from the file explorer.
   * Alternatively, if you already are running the minimum OS version, have Visual Studio installed, and have developer mode enabled, you may configure your Visual Studio directly via the .vsconfig file. To do this:
     * Open the Visual Studio Installer, select “More” on your product card and then "Import configuration"
     * Specify the .vsconfig file at the root of the repo and select “Review Details”

#### Manual set up

* Windows 10 1709 (16299) or later
* [Developer mode enabled](https://docs.microsoft.com/windows/uwp/get-started/enable-your-device-for-development) (optional)
* [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
   * Or use winget to install it ;) (although you may need to adjust the workloads via Tools -> Get Tools and Features...)
* [Git Large File Storage (LFS)](https://git-lfs.github.com/)
* The following workloads:
   * .NET Desktop Development
   * Universal Windows Platform Development
* Windows 11 SDK (10.0.22000.0) (Tools -> Get Tools and Features -> Individual Components)

### Building

Open `winget-create\src\WingetCreateCLI.sln` in Visual Studio and build. We currently only build using the solution; command line methods of building a VS solution should work as well.

## Testing the client

### Running Unit and E2E Tests

Running unit and E2E tests are a great way to ensure that functionality is preserved across major changes. You can run these tests in Visual Studio Test Explorer.

### Testing Prerequisites

* Fork the [winget-pkgs-submission-test repository](https://github.com/microsoft/winget-pkgs-submission-test)
* Fill out the test parameters in the `WingetCreateTests/Test.runsettings` file
    *  `WingetPkgsTestRepoOwner`: The repository owner of the winget-pkgs-submission-test repo. (Repo owner must be forked from main "winget-pkgs-submission-test" repo)
    *  `WingetPkgsTestRepo`: The winget-pkgs test repository. (winget-pkgs-submission-test)

* Set the solution wide runsettings file for the tests
    * Go to `Test` menu > `Configure Run Settings` -> `Select Solution Wide runsettings File` -> Choose your configured runsettings file

* Set up your github token:
    * __[Recommended]__ Run `wingetcreate token -s` to go through the Github authentication flow
    * Or create a personal access token with the `repo` permission and set it as an environment variable `WINGET_CREATE_GITHUB_TOKEN`. _(This option is more convenient for CI/CD pipelines.)_

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com. More
information is available in our [CONTRIBUTING.md](/CONTRIBUTING.md) file.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information, please refer to the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Data/Telemetry

The wingetcreate.exe client is instrumented to collect usage and diagnostic (error) data and sends it to Microsoft to help improve the product.

If you build the client yourself the instrumentation will not be enabled and no data will be sent to Microsoft.

The wingetcreate.exe client respects machine wide privacy settings and users can opt-out on their device, as documented in the Microsoft Windows privacy statement [here](https://support.microsoft.com/help/4468236/diagnostics-feedback-and-privacy-in-windows-10-microsoft-privacy).

In short to opt-out, do one of the following:

**Windows 11**: Go to `Start`, then select `Settings` > `Privacy & security` > `Diagnostics & feedback` > `Diagnostic data` and unselect `Send optional diagnostic data`.

**Windows 10**: Go to `Start`, then select `Settings` > `Privacy` > `Diagnostics & feedback`, and select `Required diagnostic data`.

You can also opt-out of telemetry by configuring the `settings.json` file and setting the `telemetry.disabled` field to true. More information can be found in our [Settings Command documentation](/doc/settings.md)

See the [privacy statement](/PRIVACY.md) for more details.

## Known Issues

Certain functionalities of wingetcreate, particularly input prompting, may not be fully supported on certain shells such as PowerShell ISE. The supported shells for the prompting package utilized by wingetcreate are specified [here](https://github.com/shibayan/Sharprompt#supported-platforms)
