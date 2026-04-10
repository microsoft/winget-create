# new-locale command (Winget-Create)

The **new-locale** command of the [Winget-Create](../README.md) tool is designed to create a new locale for an existing manifest from the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/). This command offers an interactive flow, prompting user for a set of locale fields and then generating a new locale manifest.

## Usage

Add a new locale for the latest version of a package:

`wingetcreate.exe new-locale <PackageIdentifier> --token <GitHubPersonalAccessToken>`

Add a new locale for a specific version of a package:

`wingetcreate.exe new-locale <PackageIdentifier> --token <GitHubPersonalAccessToken> --version <Version>`

Create a new locale and save the generated manifests to a specified directory:

`wingetcreate.exe new-locale <PackageIdentifier> --out <OutputDirectory> --token <GitHubPersonalAccessToken> --version <Version>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo.
| **-v, --version** |  The version of the package to add a new locale for. Default is the latest version.
| **-l, --locale** |  The package locale to create a new manifest for. If not provided, the tool will prompt you for this value.
| **-r, --reference-locale** | Existing locale manifest to be used as reference for default values. If not provided, the default locale manifest will be used.
| **-o, --out** |  The output directory where the newly created manifests will be saved locally.
| **-f, --format** |  Output format of the manifest. Default is "yaml". |
| **-t, --token**  | GitHub personal access token used for direct submission to the Windows Package Manager repo. <br/>⚠️ _Using this argument may result in the token being logged. Consider an alternative approach https://aka.ms/winget-create-token._ |
| **-n, --no-open** |  Boolean value that controls whether the pull request should not be open in the browser on submission. Default is false, meaning the PR will be opened in the browser. |
| **-?, --help** |  Gets additional help on this command |

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).
