
# update command (Winget-Create)

The **update** command of the [Winget-Create](../README.md) tool is designed to update an existing manifest. The **update** command supports both an interactive mode and an autonomous (non-interactive) mode. The interactive mode will prompt for user input offering a guided experience. The autonomous mode is designed to be used in a CI/CD pipeline to assist with automating the process of updating your package manifest. The **update** command will update the manifest with the new URL, hash and version and can automatically submit the pull request (PR) to the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/).

## Usage

`wingetcreate.exe update <id> [-u <urls>] [-v <version>] [-s] [-t <token>] [-o <output directory>] [-p <pull request title>] [-r] [<replace version>]`

The **update** command can be called with the installer URL(s) that you wish to update the manifest with. **Please make sure that the number of installer URL(s) included matches the number of existing installer nodes in the manifest you are updating. Otherwise, the command will fail.** This is to ensure that we can deterministically update each installer node with the correct matching installer url provided.

> **Note**\
> The [show](show.md) command can be used to quickly view an existing manifest from the packages repository.

### *How does Winget-Create know which installer(s) to match when executing an update?*

[Winget-Create](../README.md) will attempt to match installers based on the installer architecture and installer type. The installer type will always be derived from downloading and analyzing the installer package.

There are cases where the intended architecture specified in the existing manifest can sometimes differ from the actual architecture of the installer package. To mitigate this discrepancy, the installer architecture will first be determined by performing a regex string match to identify the possible architecture in the installer url. If no match is found, [Winget-Create](../README.md) will resort to obtaining the architecture from the downloaded installer.

## Usage Examples

Search for an existing manifest and update the version:

`wingetcreate.exe update --version <Version> <PackageIdentifier>`

Search for an existing manifest and update the installer url:

`wingetcreate.exe update --urls <InstallerUrl1> <InstallerUrl2> <PackageIdentifier>`

Save and publish updated manifest:

`wingetcreate.exe update --out <OutputDirectory> --token <GitHubPersonalAccessToken> --version <Version> <PackageIdentifier>`

Override the architecture of an installer:

`wingetcreate.exe update --urls "<InstallerUrl1>|<InstallerArchitecture>" --version <Version> <PackageIdentifier>`

Override the scope of an installer:
`wingetcreate.exe update --urls "<InstallerUrl1>|<InstallerScope>" --version <Version> <PackageIdentifier>`

> **Note**\
> The <kbd>|</kbd> character is interpreted as the pipeline operator in most shells. To use the overrides, you should wrap the installer url in quotes.

Update an existing manifest and submit PR to GitHub:

`wingetcreate.exe update --submit --token <GitHubPersonalAccessToken> --urls <InstallerUrl1> <InstallerUrl2> --version <Version> <PackageIdentifier>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo.
| **-u, --urls** |  Installer Url(s) used to extract relevant metadata for generating a manifest
| **-v, --version** |  Version to be used when updating the package version field.
| **-i, --interactive** |  Boolean value for making the update command interactive. If true, the tool will prompt the user for input. Default is false.
| **-o, --out** |  The output directory where the newly created manifests will be saved locally
| **-s, --submit** |  Boolean value for submitting to the Windows Package Manager repo. If true, updated manifest will be submitted directly using the provided GitHub Token
| **-r, --replace** |  Boolean value for replacing an existing manifest from the Windows Package Manager repo. Optionally provide a version or else the latest version will be replaced. Default is false.
| **-p, --prtitle** |  The title of the pull request submitted to GitHub.
| **-t, --token** |  GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials.
| **-?, --help** |  Gets additional help on this command. |

## Submit

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line along with the **--submit** flag, **Winget-Create** will automatically submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/).

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).

## Output

If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
