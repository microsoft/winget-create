// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateE2ETests
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateTests;
    using Microsoft.WingetCreateUnitTests;
    using NUnit.Framework;

    /// <summary>
    /// This class tests the entire end-to-end flow of the tool, from submitting a PR, retrieving the package from the repo
    /// and updating the package into a multifile manifest.
    /// </summary>
    [TestFixture]
    public class E2ETests : GitHubTestsBase
    {
        private const string PackageVersion = "1.2.3.4";
        private GitHub gitHub;

        /// <summary>
        /// One-time function that runs at the beginning of this test class.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            Logger.Initialize();
            this.gitHub = new GitHub(this.GitHubApiKey, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
            TestUtils.InitializeMockDownload();
        }

        /// <summary>
        /// E2E test of the submit and update command using a test installer.
        /// </summary>
        /// <param name="packageId">The id used for looking up an existing manifest in the repository.</param>
        /// <param name="manifestName">Manifest to convert and submit.</param>
        /// <param name="installerName">The installar package associated with the manifest.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
        [TestCase(TestConstants.TestExePackageIdentifier, TestConstants.TestExeManifest, TestConstants.TestExeInstaller)]
        [TestCase(TestConstants.TestMsiPackageIdentifier, TestConstants.TestMsiManifest, TestConstants.TestMsiInstaller)]
        [TestCase(TestConstants.TestMultifileMsixPackageIdentifier, TestConstants.TestMultifileMsixManifestDir, TestConstants.TestMsixInstaller)]
        public async Task SubmitAndUpdateInstaller(string packageId, string manifestName, string installerName)
        {
            await this.RunSubmitAndUpdateFlow(packageId, TestUtils.GetTestFile(manifestName), installerName);
        }

        /// <summary>
        /// Helper method for running the E2E test flow for the submit and update commands.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        private async Task RunSubmitAndUpdateFlow(string packageId, string manifestPath, string installerName)
        {
            SubmitCommand submitCommand = new SubmitCommand
            {
                GitHubToken = this.GitHubApiKey,
                WingetRepo = this.WingetPkgsTestRepo,
                WingetRepoOwner = this.WingetPkgsTestRepoOwner,
                Path = manifestPath,
                SubmitPRToFork = this.SubmitPRToFork,
                OpenPRInBrowser = false,
            };
            Assert.IsTrue(await submitCommand.Execute(), "Command should have succeeded");

            await this.gitHub.MergePullRequest(submitCommand.PullRequestNumber);

            TestUtils.SetMockHttpResponseContent(installerName);
            UpdateCommand updateCommand = new UpdateCommand
            {
                Id = packageId,
                GitHubToken = this.GitHubApiKey,
                SubmitToGitHub = true,
                WingetRepo = this.WingetPkgsTestRepo,
                WingetRepoOwner = this.WingetPkgsTestRepoOwner,
                SubmitPRToFork = this.SubmitPRToFork,
                OpenPRInBrowser = false,
            };

            Assert.IsTrue(await updateCommand.LoadGitHubClient(), "Failed to create GitHub client");
            Assert.IsTrue(await updateCommand.Execute(), "Command should execute successfully");

            string pathToValidate = Path.Combine(Directory.GetCurrentDirectory(), Utils.GetAppManifestDirPath(packageId, PackageVersion));
            (bool success, string message) = WinGetUtil.ValidateManifest(pathToValidate);
            Assert.IsTrue(success, message);

            await this.gitHub.ClosePullRequest(updateCommand.PullRequestNumber);
        }
    }
}
