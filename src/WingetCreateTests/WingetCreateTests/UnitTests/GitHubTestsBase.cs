// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCore.Common;
    using NUnit.Framework;

    /// <summary>
    /// Base class for unit tests which need to interact with GitHub.
    /// </summary>
    public class GitHubTestsBase
    {
        /// <summary>
        /// Gets or sets repo owner to use for unit tests.
        /// </summary>
        protected string WingetPkgsTestRepoOwner { get; set; }

        /// <summary>
        /// Gets or sets repo to use for unit tests.
        /// </summary>
        protected string WingetPkgsTestRepo { get; set; }

        /// <summary>
        /// Gets or sets gitHub PAT to use for unit tests.
        /// </summary>
        protected string GitHubApiKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether value indicating whether or not to submit the PR via a fork. Should be true when submitting as a user, false when submitting as an app.
        /// </summary>
        protected bool SubmitPRToFork { get; set; }

        /// <summary>
        /// Setup for the GitHub.cs unit tests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [OneTimeSetUp]
        public async Task SetupBase()
        {
            this.WingetPkgsTestRepoOwner = TestContext.Parameters.Get("WingetPkgsTestRepoOwner") ?? throw new ArgumentNullException("WingetPkgsTestRepoOwner must be set in runsettings file");
            this.WingetPkgsTestRepo = TestContext.Parameters.Get("WingetPkgsTestRepo") ?? throw new ArgumentNullException("WingetPkgsTestRepo must be set in runsettings file");

            if (this.WingetPkgsTestRepo == BaseCommand.DefaultWingetRepo && this.WingetPkgsTestRepoOwner == BaseCommand.DefaultWingetRepoOwner)
            {
                throw new ArgumentException($"Invalid configuration specified, you can not run tests against default repo {BaseCommand.DefaultWingetRepoOwner}/{BaseCommand.DefaultWingetRepo}");
            }

            string gitHubApiKey = TestContext.Parameters.Get("GitHubApiKey");
            string gitHubAppPrivateKey = TestContext.Parameters.Get("GitHubAppPrivateKey");

            if (!string.IsNullOrEmpty(gitHubApiKey))
            {
                TestContext.Progress.WriteLine("Using GitHubApiKey value for tests");
                this.GitHubApiKey = gitHubApiKey;
                this.SubmitPRToFork = true;
            }
            else if (!string.IsNullOrEmpty(gitHubAppPrivateKey))
            {
                TestContext.Progress.WriteLine("Using GitHubAppPrivateKey value for tests");
                TestContext.Progress.WriteLine(gitHubAppPrivateKey);
                this.GitHubApiKey = await GitHub.GetGitHubAppInstallationAccessToken(gitHubAppPrivateKey, Constants.GitHubAppId, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
                this.SubmitPRToFork = false;
            }
            else
            {
                throw new ArgumentNullException("Either GitHubApiKey or GitHubAppPrivateKey must be set in runsettings file");
            }
        }
    }
}
