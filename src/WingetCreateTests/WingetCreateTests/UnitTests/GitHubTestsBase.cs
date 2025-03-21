// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCore.Common;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

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
            // Ensure keys are not set in runsettings file
            ClassicAssert.True(string.IsNullOrEmpty(TestContext.Parameters.Get("GitHubApiKey")), "GitHubApiKey should not be set in runsettings file");
            ClassicAssert.True(string.IsNullOrEmpty(TestContext.Parameters.Get("GitHubAppPrivateKey")), "GitHubAppPrivateKey should not be set in runsettings file");

            this.WingetPkgsTestRepoOwner = TestContext.Parameters.Get("WingetPkgsTestRepoOwner") ?? throw new ArgumentNullException("WingetPkgsTestRepoOwner must be set in runsettings file");
            this.WingetPkgsTestRepo = TestContext.Parameters.Get("WingetPkgsTestRepo") ?? throw new ArgumentNullException("WingetPkgsTestRepo must be set in runsettings file");

            if (this.WingetPkgsTestRepo == BaseCommand.DefaultWingetRepo && this.WingetPkgsTestRepoOwner == BaseCommand.DefaultWingetRepoOwner)
            {
                throw new ArgumentException($"Invalid configuration specified, you can not run tests against default repo {BaseCommand.DefaultWingetRepoOwner}/{BaseCommand.DefaultWingetRepo}");
            }

            string gitHubAppPrivateKey = Environment.GetEnvironmentVariable("WINGET_CREATE_APP_KEY");
            if (string.IsNullOrEmpty(gitHubAppPrivateKey))
            {
                await this.ConfigureForLocalTestsAsync();
            }
            else
            {
                await this.ConfigureForPipelineTestsAsync(gitHubAppPrivateKey);
            }
        }

        /// <summary>
        /// Configures the tests to run in a pipeline.
        /// </summary>
        /// <param name="gitHubAppPrivateKey">Github app private key.</param>
        private async Task ConfigureForPipelineTestsAsync(string gitHubAppPrivateKey)
        {
            TestContext.Progress.WriteLine("Running in pipeline, using GitHubAppPrivateKey value for tests");
            this.GitHubApiKey = await GitHub.GetGitHubAppInstallationAccessToken(gitHubAppPrivateKey, Constants.GitHubAppId, this.WingetPkgsTestRepoOwner, this.WingetPkgsTestRepo);
            this.SubmitPRToFork = false;
        }

        /// <summary>
        /// Configures the tests to run locally.
        /// </summary>
        private async Task ConfigureForLocalTestsAsync()
        {
            TestContext.Progress.WriteLine("Running locally, using Github token for tests");
            if (TokenHelper.TryRead(out string token))
            {
                this.GitHubApiKey = token;
                this.SubmitPRToFork = true;
            }
            else
            {
                ClassicAssert.Fail("No GitHub token found.\n" +
                    ">> Please run 'wingetcreate token -s'\n" +
                    ">> Or set the 'WINGET_CREATE_GITHUB_TOKEN' environment variable to a valid GitHub token.");
            }
        }
    }
}
