# submit command (Winget-Create)

The **submit** command of the [Winget-Create](../README.md) tool is designed to submit an existing manifest to the [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/) automatically.
To use the **submit** command, you simply need to provide your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) and path to the manifest file to submit.

## Usage

`WingetCreateCLI.exe submit [\<options>] <PathToManifest>`

## Arguments

The following arguments are available:

| <div style="width:100px">Argument</div> | Description                                                                                                                                                          |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **-t, --token**                         | GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials. |

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line along with the **--submit** command and the device is registered with Github, **WingetCreate** will submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/en-us/windows/package-manager/).

## Output

If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
