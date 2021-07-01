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
    using Microsoft.WingetCreateCore.Models.Installer;
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
        public void OneTimeSetup()
        {
            this.testCommandEvent = new CommandExecutedEvent();
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
        /// Verifies that the update command succeeds and outputs manifests to the directory.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateCommandGitHubManifestTest()
        {
            string version = "1.2.3.4";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestPackageIdentifier, version, this.tempPath, null);

            var updatedManifests = await command.ExecuteManifestUpdate(initialManifestContent, this.testCommandEvent);
            Assert.IsTrue(updatedManifests, "Command should have succeeded");

            string manifestDir = Utils.GetAppManifestDirPath(TestConstants.TestPackageIdentifier, version);
            var updatedManifestContents = Directory.GetFiles(Path.Combine(this.tempPath, manifestDir)).Select(f => File.ReadAllText(f));
            Assert.IsTrue(updatedManifestContents.Any(), "Updated manifests were not created successfully");
            var manifestsToValidate = new Manifests();
            Serialization.DeserializeManifestContents(updatedManifestContents, manifestsToValidate);
            Assert.AreEqual(version, manifestsToValidate.VersionManifest.PackageVersion, $"Failed to update version of {TestConstants.TestPackageIdentifier}");
        }

        /// <summary>
        /// Tests the <see cref="UpdateCommand.DeserializeExistingManifestsAndUpdate"/> command, ensuring that it updates properties as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateAndVerifyUpdatedProperties()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsiInstaller);

            string version = "1.2.3.4";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestMsiPackageIdentifier, version, this.tempPath, null);

            var initialManifests = new Manifests();
            Serialization.DeserializeManifestContents(initialManifestContent, initialManifests);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();

            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstaller = updatedManifests.InstallerManifest.Installers.First();

            Assert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            Assert.AreNotEqual(initialInstaller.ProductCode, updatedInstaller.ProductCode, "ProductCode should be updated");
            Assert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that the update command succeeds when the existing manifest has multiple packages, and an equivalent number of URLs are provided to update.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateMultipleUrlManifests()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller, TestConstants.TestExeInstaller, TestConstants.TestMsiInstaller);

            string version = "1.2.3.4";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestMultipleInstallerPackageIdentifier, version, this.tempPath, null);

            var initialManifests = new Manifests();
            Serialization.DeserializeManifestContents(initialManifestContent, initialManifests);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();

            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            Assert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            Assert.AreEqual("en-US", updatedManifests.InstallerManifest.InstallerLocale, "InstallerLocale should be carried forward from existing installer root node");

            foreach (var updatedInstaller in updatedManifests.InstallerManifest.Installers)
            {
                Assert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");

                if (updatedInstaller.InstallerType == InstallerType.Msi || updatedInstaller.InstallerType == InstallerType.Msix)
                {
                    if (updatedInstaller.InstallerType == InstallerType.Msi)
                    {
                        Assert.AreNotEqual(initialInstaller.ProductCode, updatedInstaller.ProductCode, "ProductCode should be updated");
                        Assert.AreEqual(Scope.Machine, updatedInstaller.Scope, "Scope should be carried forward from existing installer");
                    }
                    else
                    {
                        Assert.AreNotEqual(initialInstaller.MinimumOSVersion, updatedInstaller.MinimumOSVersion, "MinimumOSVersion should be updated");
                        Assert.AreNotEqual(initialInstaller.PackageFamilyName, updatedInstaller.PackageFamilyName, "PackageFamilyName should be updated");
                        Assert.AreNotEqual(initialInstaller.Platform, updatedInstaller.Platform, "Platform should be updated");
                    }
                }
            }
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepency in the URL count.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithInstallerUrlCountDiscrepency()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestMultipleInstallerPackageIdentifier, null, this.tempPath, new[] { "fakeurl" });
            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUpdateDiscrepancy_Error), "Installer discrepency error should be thrown");
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepency in the package count.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithPackageCountDiscrepency()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.SingleMsixInExistingBundle", null, this.tempPath, null);
            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUpdateDiscrepancy_Error), "Installer discrepency error should be thrown");
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepency in the package types.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithUnmatchedPackages()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MismatchedMsixInExistingBundle", null, this.tempPath, null);
            var updatedManifests = await command.DeserializeExistingManifestsAndUpdate(initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUpdateDiscrepancy_Error), "Installer discrepency error should be thrown");
            Assert.That(result, Does.Contain(string.Format(Resources.MissingPackageError_Message, InstallerType.Msix, InstallerArchitecture.X86)), "Missing package error should be thrown");
        }

        private static List<string> GetInitialManifestContent(string manifestFileName)
        {
            string testFilePath = TestUtils.GetTestFile(manifestFileName);
            var initialManifestContent = new List<string> { File.ReadAllText(testFilePath) };
            return initialManifestContent;
        }

        private static (UpdateCommand UpdateCommand, List<string> InitialManifestContent) GetUpdateCommandAndManifestData(string id, string version, string outputDir, IEnumerable<string> installerUrls)
        {
            var updateCommand = new UpdateCommand
            {
                Id = id,
                Version = version,
                OutputDir = outputDir,
            };

            if (installerUrls != null)
            {
                updateCommand.InstallerUrls = installerUrls;
            }

            var initialManifestContent = GetInitialManifestContent($"{id}.yaml");

            return (updateCommand, initialManifestContent);
        }
    }
}
