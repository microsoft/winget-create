// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Serializers;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;
    using Octokit;

    /// <summary>
    /// Unit tests to verify GitHub interaction.
    /// </summary>
    public class GitHubTests : GitHubTestsBase
    {
        private const string FailedToRetrieveManifestFromId = "Failed to retrieve manifest from Id";

        private const string PullRequestFailedToGenerate = "Pull request failed to generate.";

        private const string GitHubPullRequestBaseUrl = "https://github.com/{0}/{1}/pull/";

        private const string TitleMismatch = "Pull request title does not match test title.";

        private GitHub gitHub;
        private StringWriter sw;

        /// <summary>
        /// Setup for the GitHub.cs unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            this.gitHub = new GitHub(this.GitHubApiKey, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
            Serialization.ProducedBy = "WingetCreateUnitTests";
            Serialization.ManifestSerializer = new YamlSerializer();
            Logger.Initialize();
        }

        /// <summary>
        /// Setup method for each individual test.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            this.sw = new StringWriter();
            Console.SetOut(this.sw);
        }

        /// <summary>
        /// Teardown method for each individual test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            this.sw.Dispose();
            PackageParser.SetHttpMessageHandler(null);
        }

        /// <summary>
        /// Tests exception handling for the GitHub repo manifest lookup functionality.
        /// Passes an invalid package identifier. Expected to throw an Octokit Not Found Exception.
        /// </summary>
        [Test]
        public void InvalidPackageIdentifier()
        {
            Assert.ThrowsAsync<NotFoundException>(async () => await this.gitHub.GetManifestContentAsync(TestConstants.TestInvalidPackageIdentifier), "Octokit.NotFoundException should be thrown");
        }

        /// <summary>
        /// Verifies the ability to identify matching package identifiers.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task FindMatchingPackageIdentifierAsync()
        {
            string exactMatch = await this.gitHub.FindPackageId(TestConstants.TestPackageIdentifier);
            StringAssert.AreEqualIgnoringCase(TestConstants.TestPackageIdentifier, exactMatch, "Failed to find existing package identifier");
        }

        /// <summary>
        /// Verifies that the GitHub client is able to submit a PR by verifying that the generated PR url and title match the correct pattern.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task GetLatestManifestAndSubmitPR()
        {
            Manifests manifests = new Manifests();
            string root = Constants.WingetManifestRoot;
            List<string> latestManifest = await this.gitHub.GetManifestContentAsync(TestConstants.TestPackageIdentifier);
            manifests.SingletonManifest = Serialization.DeserializeFromString<SingletonManifest>(latestManifest.First());
            Assert.That(manifests.SingletonManifest.PackageIdentifier, Is.EqualTo(TestConstants.TestPackageIdentifier), FailedToRetrieveManifestFromId);

            Octokit.PullRequest pullRequest = await this.gitHub.SubmitPullRequestAsync(manifests, this.SubmitPRToFork, root, TestConstants.TestPRTitle);
            Assert.That(TestConstants.TestPRTitle, Is.EqualTo(pullRequest.Title), TitleMismatch);
            await this.gitHub.ClosePullRequest(pullRequest.Number);
            StringAssert.StartsWith(string.Format(GitHubPullRequestBaseUrl, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo), pullRequest.HtmlUrl, PullRequestFailedToGenerate);
        }

        /// <summary>
        /// Verifies that the branch name is trimmed of whitespace when submitting a PR. Successful PR generation verifies that the branch name was trimmed.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task RemoveWhitespaceFromBranchName()
        {
            string packageId = "TestPublisher.VersionWithWhitespace";
            List<string> manifestContents = TestUtils.GetInitialManifestContent($"{packageId}.yaml");
            Manifests manifests = Serialization.DeserializeManifestContents(manifestContents);
            Assert.That(manifests.SingletonManifest.PackageIdentifier, Is.EqualTo(packageId), FailedToRetrieveManifestFromId);

            Octokit.PullRequest pullRequest = new();
            try
            {
                pullRequest = await this.gitHub.SubmitPullRequestAsync(manifests, this.SubmitPRToFork);
            }
            catch (Exception e)
            {
                Assert.Fail($"Failed to generate pull request. {e.Message}");
            }

            await this.gitHub.ClosePullRequest(pullRequest.Number);
            StringAssert.StartsWith(string.Format(GitHubPullRequestBaseUrl, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo), pullRequest.HtmlUrl, PullRequestFailedToGenerate);
        }

        /// <summary>
        /// Verifies that manifest metadata is automatically filled through GitHub's API if we have a GitHub Installer URL.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task ValidateAutoPopulatedManifestMetadata_Yaml()
        {
            var initialManifestContent = TestUtils.GetInitialMultifileManifestContent("Multifile.Yaml.GitHubAutoFillTest");
            UpdateCommand command = new UpdateCommand
            {
                Id = "Multifile.Yaml.GitHubAutoFillTest",
                Version = "1.2.3.4",
                InstallerUrls = new[]
                {
                    "https://github.com/microsoft/winget-pkgs-submission-test/releases/download/v1.0.1/WingetCreateTestExeInstaller.exe",
                },
                Format = ManifestFormat.Yaml,
                GitHubToken = this.GitHubApiKey,
            };
            ClassicAssert.IsTrue(await command.LoadGitHubClient(), "Failed to create GitHub client");
            Manifests initialManifests = command.DeserializeManifestContentAndApplyInitialUpdate(initialManifestContent);
            Manifests updatedManifests = await command.UpdateManifestsAutonomously(initialManifests);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            Assert.That(updatedManifests.DefaultLocaleManifest.License, Is.EqualTo("MIT"), "Could not populate License field");
            Assert.That(updatedManifests.DefaultLocaleManifest.ShortDescription, Is.EqualTo("Mirror of winget-pkgs for testing submission"), "Could not populate ShortDescription field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PackageUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test"), "Could not populate PackageUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PublisherUrl, Is.EqualTo("https://github.com/microsoft"), "Could not populate PublisherUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PublisherSupportUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/issues"), "Could not populate PublisherSupportUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.ReleaseNotesUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/releases/tag/v1.0.1"), "Could not populate ReleaseNotesUrl field");

            // ReleaseDateTime needs to be set a workaround as our YAML serializer has trouble with ReleaseDate field
            Assert.That(updatedManifests.InstallerManifest.ReleaseDateTime, Is.EqualTo("2024-08-06"), "Could not populate ReleaseDateTime field");

            List<string> expectedTags = new()
            {
                "winget-pkgs",
                "winget-create",
                "winget-pkgs-submission-test",
            };
            Assert.That(updatedManifests.DefaultLocaleManifest.Tags, Is.EquivalentTo(expectedTags), "Could not populate Tags field");
            Assert.That(updatedManifests.DefaultLocaleManifest.Documentations[0].DocumentLabel, Is.EqualTo("Wiki"), "Could not populate DocumentLabel field");
            Assert.That(updatedManifests.DefaultLocaleManifest.Documentations[0].DocumentUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/wiki"), "Could not populate DocumentUrl field");
        }

        /// <summary>
        /// Verifies that manifest metadata is automatically filled through GitHub's API if we have a GitHub Installer URL.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task ValidateAutoPopulatedManifestMetadata_Json()
        {
            var initialManifestContent = TestUtils.GetInitialMultifileManifestContent("Multifile.Json.GitHubAutoFillTest");
            UpdateCommand command = new UpdateCommand
            {
                Id = "Multifile.Yaml.GitHubAutoFillTest",
                Version = "1.2.3.4",
                InstallerUrls = new[]
                {
                    "https://github.com/microsoft/winget-pkgs-submission-test/releases/download/v1.0.1/WingetCreateTestExeInstaller.exe",
                },
                Format = ManifestFormat.Json,
                GitHubToken = this.GitHubApiKey,
            };
            ClassicAssert.IsTrue(await command.LoadGitHubClient(), "Failed to create GitHub client");
            Manifests initialManifests = command.DeserializeManifestContentAndApplyInitialUpdate(initialManifestContent);
            Manifests updatedManifests = await command.UpdateManifestsAutonomously(initialManifests);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            Assert.That(updatedManifests.DefaultLocaleManifest.License, Is.EqualTo("MIT"), "Could not populate License field");
            Assert.That(updatedManifests.DefaultLocaleManifest.ShortDescription, Is.EqualTo("Mirror of winget-pkgs for testing submission"), "Could not populate ShortDescription field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PackageUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test"), "Could not populate PackageUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PublisherUrl, Is.EqualTo("https://github.com/microsoft"), "Could not populate PublisherUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.PublisherSupportUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/issues"), "Could not populate PublisherSupportUrl field");
            Assert.That(updatedManifests.DefaultLocaleManifest.ReleaseNotesUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/releases/tag/v1.0.1"), "Could not populate ReleaseNotesUrl field");

            DateTimeOffset expectedReleaseDate = DateTimeOffset.Parse("2024-08-06 01:43:15+00:00");
            Assert.That(updatedManifests.InstallerManifest.ReleaseDate, Is.EqualTo(expectedReleaseDate), "Could not populate ReleaseDateTime field");

            List<string> expectedTags = new()
            {
                "winget-pkgs",
                "winget-create",
                "winget-pkgs-submission-test",
            };
            Assert.That(updatedManifests.DefaultLocaleManifest.Tags, Is.EquivalentTo(expectedTags), "Could not populate Tags field");
            Assert.That(updatedManifests.DefaultLocaleManifest.Documentations[0].DocumentLabel, Is.EqualTo("Wiki"), "Could not populate DocumentLabel field");
            Assert.That(updatedManifests.DefaultLocaleManifest.Documentations[0].DocumentUrl, Is.EqualTo("https://github.com/microsoft/winget-pkgs-submission-test/wiki"), "Could not populate DocumentUrl field");
        }
    }
}
