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
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying that the "update" command is working as expected.
    /// </summary>
    public class UpdateCommandTests
    {
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
            PackageParser.SetHttpMessageHandler(null);
        }

        /// <summary>
        /// Verifies that the update command succeeds and outputs manifests to the directory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateCommandTest()
        {
            string version = "1.2.3.4";
            UpdateCommand command = GetUpdateCommand(TestConstants.TestPackageIdentifier, version, this.tempPath);
            var initialManifestContent = GetInitialManifestContent($"{TestConstants.TestPackageIdentifier}.yaml");

            var updatedManifests = await command.ExecuteManifestUpdate(initialManifestContent, this.testCommandEvent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            string manifestDir = Utils.GetAppManifestDirPath(TestConstants.TestPackageIdentifier, version);
            var updatedManifestContents = Directory.GetFiles(Path.Combine(this.tempPath, manifestDir)).Select(f => File.ReadAllText(f));
            Assert.IsTrue(updatedManifestContents.Any(), "Updated manifests were not created successfully");
            Manifests manifestsToValidate = Serialization.DeserializeManifestContents(updatedManifestContents);
            Assert.AreEqual(version, manifestsToValidate.VersionManifest.PackageVersion, $"Failed to update version of {TestConstants.TestPackageIdentifier}");
        }

        /// <summary>
        /// Tests the update command to ensure that updating a multiple installer manifests should throw an error.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateWithMultipleInstallers()
        {
            UpdateCommand command = GetUpdateCommand(TestConstants.TestMultipleInstallerPackageIdentifier, null, this.tempPath);
            var initialManifestContent = GetInitialManifestContent($"{TestConstants.TestMultipleInstallerPackageIdentifier}.yaml");
            Assert.IsFalse(await command.ExecuteManifestUpdate(initialManifestContent, this.testCommandEvent), "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUrlFound_Error), "Multiple installer url error should be thrown");
        }

        /// <summary>
        /// Tests the <see cref="UpdateCommand.DeserializeExistingManifestsAndUpdate"/> command, ensuring that it updates properties as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateAndVerifyUpdatedProperties()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestMsiInstaller);

            string version = "1.2.3.4";
            UpdateCommand command = GetUpdateCommand(TestConstants.TestMsiPackageIdentifier, version, this.tempPath);
            List<string> initialManifestContent = GetInitialManifestContent(TestConstants.TestMsiManifest);
            Manifests initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();

            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstaller = updatedManifests.InstallerManifest.Installers.First();

            Assert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            Assert.AreNotEqual(initialInstaller.ProductCode, updatedInstaller.ProductCode, "ProductCode should be updated");
            Assert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        private static List<string> GetInitialManifestContent(string manifestFileName)
        {
            string testFilePath = TestUtils.GetTestFile(manifestFileName);
            var initialManifestContent = new List<string> { File.ReadAllText(testFilePath) };
            return initialManifestContent;
        }

        private static UpdateCommand GetUpdateCommand(string id, string version, string outputDir)
        {
            return new UpdateCommand
            {
                Id = id,
                Version = version,
                OutputDir = outputDir,
            };
        }
    }
}
