
# update command (Winget-Create)

The **update** command of the [Winget-Create](../README.md) tool is designed to update an existing manifest. The **update** command is non-interactive so that it can be seamlessly integrated into your build pipeline to assist with the publishing of your installer.  The **update** command will update the manifest with the new URL, hash and version and automatically submit the pull request (PR) to the [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/).  

## Usage

`WingetCreateCLI.exe update [<url>] [\<options>]`

The **update** command can be called with the optional URL.  If the URL is provided, **Winget-Create** will download the installer as it begins.  If the URL is not included, the user will need to add it when prompted.

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **-i, --id** |  Required. Package identifier used to lookup the existing manifest on the Windows Package Manager repo. Id is case-sensitive.
| **-v, --version** |  Version to be used when updating the package version field.
| **-u, --url** |  Installer Url used to extract relevant metadata for generating a manifest  
| **-o, --out** |  The output directory where the newly created manifests will be saved locally
| **-s, --submit** |  Boolean value for submitting to the Windows Package Manager repo. If true, updated manifest will be submitted directly using the provided GitHub Token
| **-t, --token** |  GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials.
| **-?, --help** |  Gets additional help on this command. |

## Winget-Create update Command flow
The update command allows you to quickly and easily update your manifest and submit a PR. **Winget-Create** can be integrated into your build pipeline to respond to your publishing of your installer.
1) publish your installer to known URL
2) call the WingetCreateCLI.exe

`WingetCreateCLI.exe update --id <PackageIdentifier> --url <InstallerUrl> --token <token> --version <version>`

### PackageIdentifier  

The first action **Winget-Create** will do is to download the metadata associated with the **PackageIdentifier**.

**Winget-Create** will then replace the following manifest values with the values provided on the command line:
* Version
* Url

Finally, **Winget-Create** will calculate and update the hash.

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line along with the **--submit** command and the device is registered with Github, **Winget-Create** will submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/).  

## Output 
If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
