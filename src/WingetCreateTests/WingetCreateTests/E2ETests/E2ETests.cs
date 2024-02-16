// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateE2ETests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateTests;
    using Microsoft.WingetCreateUnitTests;
    using NUnit.Framework;
    using Octokit;
    using Polly;

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
        /// <param name="installerName">The installer package associated with the manifest.</param>
        /// <param name="format">The format of the manifest file.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
        // YAML E2E Tests
        [TestCase(TestConstants.YamlConstants.TestExePackageIdentifier, TestConstants.YamlConstants.TestExeManifest, TestConstants.TestExeInstaller, TestConstants.YamlManifestFormat)]
        [TestCase(TestConstants.YamlConstants.TestMsiPackageIdentifier, TestConstants.YamlConstants.TestMsiManifest, TestConstants.TestMsiInstaller, TestConstants.YamlManifestFormat)]
        [TestCase(TestConstants.YamlConstants.TestMultifileMsixPackageIdentifier, TestConstants.YamlConstants.TestMultifileMsixManifestDir, TestConstants.TestMsixInstaller, TestConstants.YamlManifestFormat)]
        [TestCase(TestConstants.YamlConstants.TestPortablePackageIdentifier, TestConstants.YamlConstants.TestPortableManifest, TestConstants.TestExeInstaller, TestConstants.YamlManifestFormat)]
        [TestCase(TestConstants.YamlConstants.TestZipPackageIdentifier, TestConstants.YamlConstants.TestZipManifest, TestConstants.TestZipInstaller, TestConstants.YamlManifestFormat)]

        // JSON E2E Tests
        [TestCase(TestConstants.JsonConstants.TestExePackageIdentifier, TestConstants.JsonConstants.TestExeManifest, TestConstants.TestExeInstaller, TestConstants.JsonManifestFormat)]
        [TestCase(TestConstants.JsonConstants.TestMsiPackageIdentifier, TestConstants.JsonConstants.TestMsiManifest, TestConstants.TestMsiInstaller, TestConstants.JsonManifestFormat)]
        [TestCase(TestConstants.JsonConstants.TestMultifileMsixPackageIdentifier, TestConstants.JsonConstants.TestMultifileMsixManifestDir, TestConstants.TestMsixInstaller, TestConstants.JsonManifestFormat)]
        [TestCase(TestConstants.JsonConstants.TestPortablePackageIdentifier, TestConstants.JsonConstants.TestPortableManifest, TestConstants.TestExeInstaller, TestConstants.JsonManifestFormat)]
        [TestCase(TestConstants.JsonConstants.TestZipPackageIdentifier, TestConstants.JsonConstants.TestZipManifest, TestConstants.TestZipInstaller, TestConstants.JsonManifestFormat)]

        public async Task SubmitAndUpdateInstaller(string packageId, string manifestName, string installerName, ManifestFormat format)
        {
            await this.RunSubmitAndUpdateFlow(packageId, TestUtils.GetTestFile(manifestName), installerName, format);
        }

        /// <summary>
        /// Helper method for running the E2E test flow for the submit and update commands.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        private async Task RunSubmitAndUpdateFlow(string packageId, string manifestPath, string installerName, ManifestFormat format)
        {
            string pullRequestTitle = $"{packageId} {PackageVersion} ({format})";
            Serialization.SetManifestSerializer(format.ToString());
            SubmitCommand submitCommand = new SubmitCommand
            {
                GitHubToken = this.GitHubApiKey,
                WingetRepo = this.WingetPkgsTestRepo,
                WingetRepoOwner = this.WingetPkgsTestRepoOwner,
                Path = manifestPath,
                PRTitle = pullRequestTitle,
                SubmitPRToFork = this.SubmitPRToFork,
                Format = format,
                OpenPRInBrowser = false,
            };
            Assert.IsTrue(await submitCommand.Execute(), "Command should have succeeded");

            var mergeRetryPolicy = Policy
                .Handle<PullRequestNotMergeableException>()
                .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));

            await mergeRetryPolicy.ExecuteAsync(async () =>
            {
                // Attempting to merge immediately after creating the PR can throw an exception if the branch
                // has not completed verification if it can be merged. Wait and retry.
                await this.gitHub.MergePullRequest(submitCommand.PullRequestNumber);
            });

            TestUtils.SetMockHttpResponseContent(installerName);
            UpdateCommand updateCommand = new UpdateCommand
            {
                Id = packageId,
                GitHubToken = this.GitHubApiKey,
                SubmitToGitHub = true,
                WingetRepo = this.WingetPkgsTestRepo,
                WingetRepoOwner = this.WingetPkgsTestRepoOwner,
                SubmitPRToFork = this.SubmitPRToFork,
                PRTitle = pullRequestTitle,
                Format = format,
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
