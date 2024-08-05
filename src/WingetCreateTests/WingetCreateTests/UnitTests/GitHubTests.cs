// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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

        /// <summary>
        /// Setup for the GitHub.cs unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            this.gitHub = new GitHub(this.GitHubApiKey, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
            Serialization.ProducedBy = "WingetCreateUnitTests";
            Serialization.ManifestSerializer = new YamlSerializer();
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
            List<string> latestManifest = await this.gitHub.GetManifestContentAsync(TestConstants.TestPackageIdentifier);
            manifests.SingletonManifest = Serialization.DeserializeFromString<SingletonManifest>(latestManifest.First());
            Assert.That(manifests.SingletonManifest.PackageIdentifier, Is.EqualTo(TestConstants.TestPackageIdentifier), FailedToRetrieveManifestFromId);

            PullRequest pullRequest = await this.gitHub.SubmitPullRequestAsync(manifests, this.SubmitPRToFork, TestConstants.TestPRTitle);
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

            PullRequest pullRequest = new();
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
        public async Task ValidateAutoPopulatedManifestMetadata()
        {
            Manifests manifests = new Manifests
            {
                InstallerManifest = new(),
                DefaultLocaleManifest = new(),
                LocaleManifests = new(),
                VersionManifest = new(),
            };

            manifests.InstallerManifest.Installers.Add(new()
            {
                InstallerUrl = "https://github.com/microsoft/PowerToys/releases/download/v0.82.1/PowerToysUserSetup-0.82.1-x64.exe",
                InstallerSha256 = "C346622DD15FECA8184D9BAEAC73B80AB96007E6B12CAAD46A055FE44A5A3908",
            });

            await this.gitHub.PopulateGitHubMetadata(manifests, "yaml");
            Assert.That(manifests.DefaultLocaleManifest.License, Is.EqualTo("MIT"), "Could not populate License field");
            Assert.That(manifests.DefaultLocaleManifest.ShortDescription, Is.EqualTo("Windows system utilities to maximize productivity"), "Could not populate ShortDescription field");
            Assert.That(manifests.DefaultLocaleManifest.PackageUrl, Is.EqualTo("https://github.com/microsoft/PowerToys"), "Could not populate PackageUrl field");
            Assert.That(manifests.DefaultLocaleManifest.PublisherUrl, Is.EqualTo("https://github.com/microsoft"), "Could not populate PublisherUrl field");
            Assert.That(manifests.DefaultLocaleManifest.PublisherSupportUrl, Is.EqualTo("https://github.com/microsoft/PowerToys/issues"), "Could not populate PublisherSupportUrl field");
            Assert.That(manifests.DefaultLocaleManifest.ReleaseNotesUrl, Is.EqualTo("https://github.com/microsoft/PowerToys/releases/tag/v0.82.1"), "Could not populate ReleaseNotesUrl field");
            Assert.That(manifests.InstallerManifest.ReleaseDateTime, Is.EqualTo("2024-07-12"), "Could not populate ReleaseDate field");

            List<string> expectedTags = new()
            {
                "windows",
                "color-picker",
                "desktop",
                "keyboard-manager",
                "powertoys",
                "fancyzones",
                "microsoft-powertoys",
                "powerrename",
            };
            Assert.That(manifests.DefaultLocaleManifest.Tags, Is.EquivalentTo(expectedTags), "Could not populate Tags field");
            Assert.That(manifests.DefaultLocaleManifest.Documentations[0].DocumentLabel, Is.EqualTo("Wiki"), "Could not populate DocumentLabel field");
            Assert.That(manifests.DefaultLocaleManifest.Documentations[0].DocumentUrl, Is.EqualTo("https://github.com/microsoft/PowerToys/wiki"), "Could not populate DocumentUrl field");
        }
    }
}
