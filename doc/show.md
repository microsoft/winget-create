# show command (Winget-Create)

The **show** command of the [Winget-Create](../README.md) tool is designed to show an existing manifest from the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/). This command is particularly useful when you need to update an existing package and wish to examine an older manifest without leaving the CLI. This helps you determine the count of existing installer nodes and decide if architecture or scope overrides are necessary for the update.

## Usage

Show the latest manifest of an existing package from the Windows Package Manager repo:

`wingetcreate.exe show <PackageIdentifier> --token <GitHubPersonalAccessToken>`

Show a specified version of a package manifest:

`wingetcreate.exe show <PackageIdentifier> --version <PackageVersion> --token <GitHubPersonalAccessToken>`

Show only the installer and default locale manifest:

`wingetcreate.exe show <PackageIdentifier> --installer-manifest --defaultlocale-manifest --token <GitHubPersonalAccessToken>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **-v, --version** |  The version of the existing package manifest from the Windows Package Manager Repo to retrieve the manifest for. Default is the latest version.
| **-i, --installer-manifest** |  Switch to display the installer manifest.
| **-d, --defaultlocale-manifest** |  Switch to display the default locale manifest.
| **-l, --locale-manifests** |  Switch to display all locale manifests.
| **--version-manifest** |  Switch to display the version manifest.
| **-f,--format** |  Output format of the manifest. Default is "yaml". |
| **-t, --token** |  GitHub personal access token used for authenticated access to the GitHub API. It is recommended to provide a token to get a higher [API rate limit](https://docs.github.com/en/rest/overview/resources-in-the-rest-api#rate-limiting). <br/>⚠️ _Using this argument may result in the token being logged. Consider an alternative approach https://aka.ms/winget-create-token._ |
| **-?, --help** |  Gets additional help on this command. |

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).
