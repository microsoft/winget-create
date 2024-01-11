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
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;
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
    }
}
