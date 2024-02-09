# token command (Winget-Create)

The **token** command of the [Winget-Create](../README.md) tool is designed to manage cached GitHub personal access tokens used by the tool for interacting with the [Windows Package Manager repo](https://docs.microsoft.com/windows/package-manager/) automatically.
To use the **token** command, you can specify whether you want to store a new [GitHub token](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) or clear any existing cached tokens. If you choose not to provide a token when storing, the tool will initiate an OAuth flow and prompt for your GitHub login credentials.

Instructions on setting up GitHub Token for Winget-Create can be found [here](../README.md#github-personal-access-token-classic-permissions).

## Usage

`wingetcreate.exe token [\<options>]`

### Store a new GitHub token in your local cache

`wingetcreate.exe token --store --token <GitHubPersonalAccessToken>`

### Clear the cached GitHub token

`wingetcreate.exe token --clear`

## Arguments

The following arguments are available:

| <div style="width:100px">Argument</div>| Description |
|----------------  |-------------|
| **-c, --clear**  | Required. Clear the cached GitHub token
| **-s, --store**  | Required. Set the cached GitHub token. Can specify token to cache with --token parameter, otherwise will initiate OAuth flow.
| **-t, --token**   | GitHub personal access token used for direct submission to the Windows Package Manager repo. If no token is provided, tool will prompt for GitHub login credentials.
| **-?, --help** |  Gets additional help on this command. |
