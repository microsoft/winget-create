
# update command (Winget-Create)

The **update** command of the [Winget-Create](../README.md) tool is designed to update an existing manifest. The **update** command is non-interactive so that it can be seamlessly integrated into your build pipeline to assist with the publishing of your installer.  The **update** command will update the manifest with the new URL, hash and version and can automatically submit the pull request (PR) to the [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/).  

## Usage

`WingetCreateCLI.exe update <id> [-u <urls>] [-v <version>] [-s] [-t <token>] [-o <output directory>]`

The **update** command can be called with the installer URL(s) that you wish to update the manifest with. **Please make sure that the number of installer URL(s) included matches the number of existing installer nodes in the manifest you are updating. Otherwise, the command will fail.** This is to ensure that we can deterministically update each installer node with the correct matching installer url provided. 

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

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo.
| **-u, --urls** |  Installer Url(s) used to extract relevant metadata for generating a manifest
| **-v, --version** |  Version to be used when updating the package version field.
| **-o, --out** |  The output directory where the newly created manifests will be saved locally
| **-s, --submit** |  Boolean value for submitting to the Windows Package Manager repo. If true, updated manifest will be submitted directly using the provided GitHub Token
| **-t, --token** |  GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials.
| **-?, --help** |  Gets additional help on this command. |

## Submit 

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line along with the **--submit** command and the device is registered with Github, **Winget-Create** will automatically submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/).  

## Output 
If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
