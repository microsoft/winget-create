
# Welcome to the Windows Package Manager Manifest Creator repository.  

This repository contains the source code for the Windows Package Manager Manifest Creator.  The  Windows Package Manager Manifest Creator is designed to help generate or update manifest files for the [Community repo](https://github.com/microsoft/winget-pkgs).  

## Overview 
**Windows Package Manager Manifest Creator** is an Open Source tool designed to help developers create, update, and submit manifest files to the [Windows Package Manager repository](https://github.com/microsoft/winget-pkgs).  

Developers will use this tool to submit their applications for use with the [Windows Package Manager](https://docs.microsoft.com/en-us/windows/package-manager/).

## Getting Started
For your convenience, **Winget-Create** can be acquired a number of ways.

### Install from the github repo ###
The **Windows Package Manager Manifest Creator** is available for download from the [Winget-Create](https://github.com/microsoft/winget-create/releases) repository.  To install the package, simply click the the MSIX file in your browser.  Once it has downloaded, click open.

### Install with Windows Package Manager ###
> [!NOTE][coming soon]
winget install wingetcreate
 
## Using Windows Package Manager Manifest Creator

**Winget-Create** has the following commands:

| Command  | Description |
| -------  | ----------- |
| [New](doc/new.md)      | Command for creating a new manifest from scratch |
| [Update](doc/update.md)  | Command for updating an existing manifest |
| [Submit](doc/submit.md)  | Command for submitting an existing PR  |
| [Token](doc/token.md)   | Command for creating a new manifest from scratch |
| [-?](doc/help.md)      | Displays command line help |

Click on the individual commands to learn more.

## Building the client

### Prerequisites

* Windows 10 1709 (16299) or later
* [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/)
   * Or use winget to install it ;) (although you may need to adjust the workloads via Tools->Get Tools and Features...)
* [Git Large File Storage (LFS)](https://git-lfs.github.com/)   
* The following workloads:
   * .NET Desktop Development
   * Universal Windows Platform Development


### Building

We currently only build using the solution; command line methods of building a VS solution should work as well.

## Testing the client

### Running Unit and E2E Tests

Running unit and E2E tests are a great way to ensure that functionality is preserved across major changes. You can run these tests in Visual Studio Test Explorer. 

### Testing Prerequisites

* Fork the [winget-pkgs-submission-test repository](https://github.com/microsoft/winget-pkgs-submission-test)
* Fill out the test parameters in the WingetCreateTests/Test.runsettings file
    *  __**WingetPkgsTestRepoOwner**__: The repository owner of the winget-pkgs-submission-test repo. (Repo owner must be forked from main "winget-pkgs-submission-test" repo)
    *  __**WingetPkgsTestRepo**__: The winget-pkgs test repository. (winget-pkgs-submission-test)
    *  __**GitHubApiKey**__: GitHub personal access token for testing. 
        * Instructions on [how to generate your own GitHubApiKey](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token).

* Set the solution wide runsettings file for the tests
    * Go to TestExplorer -> Settings -> Configure Run Settings -> Select Solution-Wide runsettings file -> Choose your configured runsettings file

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com. More 
information is available in our [CONTRIBUTING.md](/CONTRIBUTING.md) file.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information, please refer to the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Known Issues
