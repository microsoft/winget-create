
# update command (Winget-Create)

The **update** command of the [Winget-Create](../README.md) tool is designed to update an existing manifest. The **update** command supports both an interactive mode and an autonomous (non-interactive) mode. The interactive mode will prompt for user input offering a guided experience. The autonomous mode is designed to be used in a CI/CD pipeline to assist with automating the process of updating your package manifest. The **update** command will update the manifest with the new URL, hash and version and can automatically submit the pull request (PR) to the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/).

## Usage

`wingetcreate.exe update <id> [-u <urls>] [-v <version>] [-s] [-t <token>] [-o <output directory>] [-p <pull request title>] [-r] [<replace version>] [-d <display version>] [--release-date <release date> ] [--release-notes-url <release notes url>] [--format <format>] [--interactive] [--help]`

The **update** command can be called with the installer URL(s) that you wish to update the manifest with. **Please make sure that the number of installer URL(s) included matches the number of existing installer nodes in the manifest you are updating. Otherwise, the command will fail.** This is to ensure that we can deterministically update each installer node with the correct matching installer url provided.

> [!NOTE]
> The [show](show.md) command can be used to quickly view an existing manifest from the packages repository.

### *How does Winget-Create know which installer(s) to match when executing an update?*

[Winget-Create](../README.md) will attempt to match installers based on the installer architecture and installer type. The installer type will always be derived from downloading and analyzing the installer package.

There are cases where the intended architecture specified in the existing manifest can sometimes differ from the actual architecture of the installer package. To mitigate this discrepancy, the installer architecture will first be determined by performing a regex string match to identify the possible architecture in the installer url. If no match is found, [Winget-Create](../README.md) will resort to obtaining the architecture from the downloaded installer.

If Winget-Create fails to detect the architecture from the binary or the detected architecture does not match an architecture in the existing manifest, Winget-Create will fail to generate the manifest. In this case, you can explicitly provide the intended architecture in the update command using the following override format:

`'<InstallerUrl>|<InstallerArchitecture>'`

e.g.,

`wingetcreate update <PackageIdentifier> --urls "<InstallerUrl1>|x64" "<InstallerUrl2>|x86"`

In case there are multiple installers with the same architecture, it may mean the same installer is available for multiple scopes. In this case, you can explicitly provide the installer scope in the update command using the following override format:

`'<InstallerUrl>|<InstallerArchitecture>|<InstallerScope>'`

e.g.,

`wingetcreate update <PackageIdentifier> --urls '<InstallerUrl1>|x64|user' '<InstallerUrl1>|x64|machine' '<InstallerUrl2>|x86|user' '<InstallerUrl2>|x86|machine'`

### Auto-filling manifest fields

If the installer URLs come from a GitHub release, the CLI can automatically fill in missing manifest metadata. A valid GitHub token must be provided using the `--token` argument to use this feature.
The update flow may automatically fill in the following fields:

- `ReleaseDate` - The publish date of the release on GitHub.
- `ReleaseNotesUrl` - The URL to the release notes on GitHub.
- `PackageUrl` - The URL to the package GitHub repository.
- `PublisherUrl` - The URL to the publisher's GitHub page.
- `PublisherSupportUrl` - The URL to GitHub issues for the package repository.
- `Tags` - The tags from the GitHub repository.
- `Documentations` - If the GitHub repository has a wiki, the URL to the wiki will be added to the manifest.

### Installer URL arguments

The following additional arguments can be provided with the installer URL(s):

#### Format

`'<InstallerUrl>|<Argument1>|<Argument2>...'`

#### Override Architecture

Winget-Create will attempt to determine the architecture of the installer package by performing a regex string match to identify the possible architecture in the installer url. If no match is found, Winget-Create will resort to obtaining the architecture from the downloaded installer. If Winget-Create fails to detect the architecture from the binary or the detected architecture does not match an architecture in the existing manifest, Winget-Create will fail to generate the manifest. In this case, you can explicitly provide the intended architecture and override the detected architecture using the following format:

`'<InstallerUrl>|<InstallerArchitecture>'`

#### Override Scope

In case there are multiple installers with the same architecture, it may mean the same installer is available for multiple scopes. In this case, you can explicitly provide the installer scope in the update command using the following following argument format:

`'<InstallerUrl>|<InstallerScope>'`

#### Display Version

In some cases, the publisher of the package may use a different marketing version than the actual version written to Apps & Features. In this case, the manifest will contain `DisplayVersion` field. You can update the `DisplayVersion` field using the `--display-version` CLI arg if all installers use the same display version. If the display version differs for each installer, you can use following argument format:

`'<InstallerUrl>|<DisplayVersion>'`

## Usage Examples

Search for an existing manifest and update the version:

`wingetcreate.exe update <PackageIdentifier> --version <Version>`

Search for an existing manifest and update the installer url:

`wingetcreate.exe update <PackageIdentifier> --urls <InstallerUrl1> <InstallerUrl2>`

Save and publish updated manifest:

`wingetcreate.exe update <PackageIdentifier> --out <OutputDirectory> --token <GitHubPersonalAccessToken> --version <Version>`

Override the architecture of an installer:

`wingetcreate.exe update <PackageIdentifier> --urls '<InstallerUrl1>|<InstallerArchitecture>' --version <Version>`

Override the scope of an installer:
`wingetcreate.exe update <PackageIdentifier> --urls '<InstallerUrl1>|<InstallerScope>' --version <Version>`

> [!NOTE]
> The <kbd>|</kbd> character is interpreted as the pipeline operator in most shells. To use the overrides, you should wrap the installer url in quotes.

Update an existing manifest and submit PR to GitHub:

`wingetcreate.exe update <PackageIdentifier> --submit --token <GitHubPersonalAccessToken> --urls <InstallerUrl1> <InstallerUrl2> --version <Version>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo. |
| **-u, --urls** |  Installer Url(s) used to extract relevant metadata for generating a manifest |
| **-v, --version** |  Version to be used when updating the package version field. |
| **-d, --display-version** | Version to be used when updating the display version field. Version provided in the installer URL arguments will take precedence over this value. |
| **--release-notes-url** |  URL to be used when updating the release notes url field. |
| **--release-date** |  Date to be used when updating the release date field. Expected format is "YYYY-MM-DD". |
| **-o, --out** |  The output directory where the newly created manifests will be saved locally |
| **-p, --prtitle** |  The title of the pull request submitted to GitHub. |
| **-s, --submit** |  Boolean value for submitting to the Windows Package Manager repo. If true, updated manifest will be submitted directly using the provided GitHub Token |
| **-r, --replace** |  Boolean value for replacing an existing manifest from the Windows Package Manager repo. Optionally provide a version or else the latest version will be replaced. Default is false. |
| **-i, --interactive** |  Boolean value for making the update command interactive. If true, the tool will prompt the user for input. Default is false. |
| **-f, --format** |  Output format of the manifest. Default is "yaml". |
| **--allow-unsecure-downloads** | Allow unsecure downloads (HTTP) for this operation. |
| **-t, --token** |  GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials. <br/>⚠️ _Using this argument may result in the token being logged. Consider an alternative approach https://aka.ms/winget-create-token._ |
| **-n, --no-open** |  Boolean value that controls whether the pull request should not be open in the browser on submission. Default is false, meaning the PR will be opened in the browser. |
| **-?, --help** |  Gets additional help on this command. |

## Submit

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line along with the **--submit** flag, **Winget-Create** will automatically submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/).

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).

## Output

If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
