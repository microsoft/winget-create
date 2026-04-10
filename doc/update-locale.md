# update-locale command (Winget-Create)

The **update-locale** command of the [Winget-Create](../README.md) tool is designed to update existing locales for a manifest from the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/). This command offers an interactive flow, prompting user for a set of locale fields and then generating a new manifest for submission.

## Usage

Update existing locales for the latest version of a package:

`wingetcreate.exe update-locale <PackageIdentifier> --token <GitHubPersonalAccessToken>`

Update existing locales for a specific version of a package:

`wingetcreate.exe update-locale <PackageIdentifier> --token <GitHubPersonalAccessToken> --version <Version>`

Update existing locale and save the generated manifests to a specified directory:

`wingetcreate.exe update-locale <PackageIdentifier> --out <OutputDirectory> --token <GitHubPersonalAccessToken> --version <Version>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo.
| **-v, --version** |  The version of the package to update the locale for. Default is the latest version.
| **-l, --locale** |  The package locale to update the manifest for. If not provided, the tool will prompt you a list of existing locales to choose from.
| **-o, --out** |  The output directory where the newly created manifests will be saved locally.
| **-f, --format** |  Output format of the manifest. Default is "yaml". |
| **-t, --token**  | GitHub personal access token used for direct submission to the Windows Package Manager repo. <br/>⚠️ _Using this argument may result in the token being logged. Consider an alternative approach https://aka.ms/winget-create-token._ |
| **-n, --no-open** |  Boolean value that controls whether the pull request should not be open in the browser on submission. Default is false, meaning the PR will be opened in the browser. |
| **-?, --help** |  Gets additional help on this command |

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).
