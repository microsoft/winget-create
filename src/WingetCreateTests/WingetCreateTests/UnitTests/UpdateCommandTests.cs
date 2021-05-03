// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying that the "update" command is working as expected.
    /// </summary>
    public class UpdateCommandTests : GitHubTestsBase
    {
        /// <summary>
        /// Verifies that the update command modifies the manifest's version field
        /// when a "Version" argument is provided.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateVersion()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);
            Logger.Initialize();
            string version = "1.2.3.4";
            string tempPath = Path.GetTempPath();
            UpdateCommand command = this.GetUpdateCommand(TestConstants.TestPackageIdentifier, version, tempPath);
            Assert.IsTrue(await command.Execute(), "Command should have succeeded");

            string manifestDir = Utils.GetAppManifestDirPath(TestConstants.TestPackageIdentifier, version);
            string fullOutputPath = Path.Combine(tempPath, manifestDir, $"{TestConstants.TestPackageIdentifier}.yaml");

            Assert.IsTrue(File.Exists(fullOutputPath), "Updated manifest was not created successfully");
            string result = File.ReadAllText(fullOutputPath);
            Assert.That(result, Does.Contain("PackageVersion: 1.2.3.4"), $"Failed to update version of {TestConstants.TestPackageIdentifier}");
        }

        /// <summary>
        /// Tests exception handling for the GitHub repo manifest lookup functionality.
        /// Passes an invalid package identifier. Expected to throw an Octokit Not Found Exception.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateWithInvalidPackageIdentifier()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);
            Logger.Initialize();

            string tempPath = Path.GetTempPath();

            Logger.Initialize();
            UpdateCommand command = this.GetUpdateCommand(TestConstants.TestInvalidPackageIdentifier, null, tempPath);
            Assert.IsFalse(await command.Execute(), "Command should have failed");
            string result = sw.ToString();
            Assert.That(result, Does.Contain(Resources.OctokitNotFound_Error), "Octokit.NotFoundException should be thrown");
        }

        private UpdateCommand GetUpdateCommand(string id, string version, string outputDir)
        {
            return new UpdateCommand
            {
                Id = id,
                Version = version,
                OutputDir = outputDir,
                GitHubToken = this.GitHubApiKey,
                WingetRepoOwner = this.WingetPkgsTestRepoOwner,
                WingetRepo = this.WingetPkgsTestRepo,
            };
        }
    }
}
