// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
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

        private GitHub gitHub;

        /// <summary>
        /// Setup for the GitHub.cs unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            this.gitHub = new GitHub(this.GitHubApiKey, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
        }

        /// <summary>
        /// Tests exception handling for the GitHub repo manifest lookup functionality.
        /// Passes an invalid package identifier. Expected to throw an Octokit Not Found Exception.
        /// </summary>
        [Test]
        public void InvalidPackageIdentifier()
        {
            Assert.ThrowsAsync<NotFoundException>(async () => await this.gitHub.GetLatestManifestContentAsync(TestConstants.TestInvalidPackageIdentifier), "Octokit.NotFoundException should be thrown");
        }

        /// <summary>
        /// Verifies that the GitHub client is able to submit a PR by verifying that the generated PR url matches the correct pattern.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task GetLatestManifestAndSubmitPR()
        {
            Manifests manifests = new Manifests();
            List<string> latestManifest = await this.gitHub.GetLatestManifestContentAsync(TestConstants.TestPackageIdentifier);
            manifests.SingletonManifest = Serialization.DeserializeFromString<SingletonManifest>(latestManifest.First());
            Assert.That(manifests.SingletonManifest.PackageIdentifier, Is.EqualTo(TestConstants.TestPackageIdentifier), FailedToRetrieveManifestFromId);

            PullRequest pullRequest = await this.gitHub.SubmitPullRequestAsync(manifests, "Winget-CLI Testing", this.SubmitPRToFork);
            await this.gitHub.ClosePullRequest(pullRequest.Number);
            StringAssert.StartsWith(string.Format(GitHubPullRequestBaseUrl, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo), pullRequest.HtmlUrl, PullRequestFailedToGenerate);
        }
    }
}
