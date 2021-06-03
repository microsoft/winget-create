// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying that the "update" command is working as expected.
    /// </summary>
    public class UpdateCommandTests : GitHubTestsBase
    {
        private const string UpdateCommandTestHeader = "WingetCreate Update Command Tests";
        private readonly string tempPath = Path.GetTempPath();
        private StringWriter sw;
        private CommandExecutedEvent testCommandEvent;

        /// <summary>
        /// Setup method for the update command unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            this.testCommandEvent = new CommandExecutedEvent();
            this.sw = new StringWriter();
            Console.SetOut(this.sw);
            Logger.Initialize();
        }

        /// <summary>
        /// Teardown method for the update command unit tests.
        /// </summary>
        [OneTimeTearDown]
        public void TearDown()
        {
            this.sw.Dispose();
        }

        /// <summary>
        /// Verifies that the update command modifies the manifest's version field
        /// when a "Version" argument is provided.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateVersion()
        {
            string version = "1.2.3.4";
            UpdateCommand command = this.GetUpdateCommand(TestConstants.TestPackageIdentifier, version, this.tempPath);
            SingletonManifest testManifest = Serialization.DeserializeFromPath<SingletonManifest>(TestUtils.GetTestFile($"{TestConstants.TestPackageIdentifier}.yaml"));
            List<string> manifests = new List<string> { testManifest.ToYaml(UpdateCommandTestHeader) };
            manifests.Add(testManifest.ToYaml(UpdateCommandTestHeader));

            Assert.IsTrue(await command.ExecuteManifestUpdate(manifests, this.testCommandEvent), "Command should have succeeded");

            string manifestDir = Utils.GetAppManifestDirPath(TestConstants.TestPackageIdentifier, version);
            string fullOutputPath = Path.Combine(this.tempPath, manifestDir, $"{TestConstants.TestPackageIdentifier}.yaml");

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
            UpdateCommand command = this.GetUpdateCommand(TestConstants.TestInvalidPackageIdentifier, null, this.tempPath);
            Assert.IsFalse(await command.Execute(), "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.OctokitNotFound_Error), "Octokit.NotFoundException should be thrown");
        }

        /// <summary>
        /// Tests the update command to ensure that updating a multiple installer manifests should throw an error.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateWithMultipleInstallers()
        {
            UpdateCommand command = this.GetUpdateCommand(TestConstants.TestMultipleInstallerPackageIdentifier, null, this.tempPath);
            SingletonManifest testManifest = Serialization.DeserializeFromPath<SingletonManifest>(TestUtils.GetTestFile($"{TestConstants.TestMultipleInstallerPackageIdentifier}.yaml"));
            List<string> manifests = new List<string> { testManifest.ToYaml(UpdateCommandTestHeader) };
            manifests.Add(testManifest.ToYaml(UpdateCommandTestHeader));
            Assert.IsFalse(await command.ExecuteManifestUpdate(manifests, this.testCommandEvent), "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUrlFound_Error), "Multiple installer url error should be thrown");
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
