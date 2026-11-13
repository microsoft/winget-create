# submit command (Winget-Create)

The **submit** command of the [Winget-Create](../README.md) tool is designed to submit an existing manifest to the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/) automatically.
To use the **submit** command, you simply need to provide your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) and path to the manifest file to submit.

## Usage

Submit local manifest file to Windows Package Manager repo:

`wingetcreate.exe submit --prtitle <PullRequestTitle> --token <GitHubPersonalAccessToken> <PathToManifest>`

## Arguments

The following arguments are available:

| Argument  | Description |
|--------------|-------------|
| **-p, --prtitle** |  The title of the pull request submitted to GitHub.
| **-r, --replace** |  Boolean value for replacing an existing manifest from the Windows Package Manager repo. Optionally provide a version or else the latest version will be replaced. Default is false.
| **-t, --token** |  GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials. <br/>⚠️ _Using this argument may result in the token being logged. Consider an alternative approach https://aka.ms/winget-create-token._ |
| **-n, --no-open** |  Boolean value that controls whether the pull request should not be open in the browser on submission. Default is false, meaning the PR will be opened in the browser. |
| **-?, --help** |  Gets additional help on this command. |

If you have provided your [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) on the command line with the **submit** command and the device is registered with GitHub, **Winget-Create** will submit your PR to [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/).

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).

## Output

If you would like to write the file to disk rather than submit to the repository, you can pass in the **--output** command along with the file name to write to.
