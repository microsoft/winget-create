﻿// Copyright (c) Microsoft. All rights reserved.
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
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
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
            Manifests manifestsToValidate = Serialization.DeserializeManifestContents(updatedManifestContents);
            Assert.AreEqual(version, manifestsToValidate.VersionManifest.PackageVersion, $"Failed to update version of {TestConstants.TestPackageIdentifier}");
        }

        /// <summary>
        /// Tests the <see cref="UpdateCommand.UpdateManifestsAutonomously"/> command, ensuring that it updates properties as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateAndVerifyUpdatedProperties()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsiInstaller);
            string version = "1.2.3.4";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestMsiPackageIdentifier, version, this.tempPath, null);

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstaller = updatedManifests.InstallerManifest.Installers.First();
            Assert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            Assert.AreNotEqual(initialInstaller.ProductCode, updatedManifests.InstallerManifest.ProductCode, "ProductCode should be updated");
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

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
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
        /// Verifies that any fields with empty string values are replaced with null so that they do not appear in the manifest output.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateRemovesEmptyFields()
        {
            string packageId = "TestPublisher.EmptyFields";
            string version = "1.2.3.4";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(packageId, version, this.tempPath, null);
            bool updateExecuted = await command.ExecuteManifestUpdate(initialManifestContent, this.testCommandEvent);
            Assert.IsTrue(updateExecuted, "Command should have succeeded");
            string manifestDir = Utils.GetAppManifestDirPath(packageId, version);
            var updatedManifestContents = Directory.GetFiles(Path.Combine(this.tempPath, manifestDir)).Select(f => File.ReadAllText(f));
            Assert.IsTrue(updatedManifestContents.Any(), "Updated manifests were not created successfully");

            Manifests updatedManifests = Serialization.DeserializeManifestContents(updatedManifestContents);
            Assert.IsNull(updatedManifests.DefaultLocaleManifest.PrivacyUrl, "PrivacyUrl should be null.");
            Assert.IsNull(updatedManifests.DefaultLocaleManifest.Author, "Author should be null.");

            var firstInstaller = updatedManifests.InstallerManifest.Installers.First();
            Assert.IsNull(firstInstaller.ProductCode, "ProductCode should be null.");
            Assert.IsNull(firstInstaller.PackageFamilyName, "ProductCode should be null.");
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
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
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
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.InstallerCountMustMatch_Error), "Installer count must match error should be thrown");
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
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.NewInstallerUrlMustMatchExisting_Message), "New installer must match error should be thrown");
        }

        /// <summary>
        /// Since some installers are incorrectly labeled on the manifest, resort to using the installer URL to find matches.
        /// This unit test uses a msi installer that is not an arm64 installer, but because the installer URL includes "arm64", it should find a match.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateWithArchDetectedFromInstallerUrl()
        {
            var archs = new[] { "arm64", "arm", "win64", "win32" };
            var expectedArchs = new[]
            {
                Architecture.Arm64,
                Architecture.Arm,
                Architecture.X64,
                Architecture.X86,
            };

            TestUtils.InitializeMockDownloads(archs.Select(a => $"{a}/{TestConstants.TestMsiInstaller}").ToArray());

            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MatchWithArchFromInstallerUrl", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            foreach (var item in expectedArchs.Zip(updatedManifests.InstallerManifest.Installers, (expectedArch, installer) => (expectedArch, installer)))
            {
                Assert.AreEqual(item.expectedArch, item.installer.Architecture, "Architecture not parsed correctly from url string");
                Assert.AreNotEqual(initialInstaller.InstallerSha256, item.installer.InstallerSha256, "InstallerSha256 should be updated");
            }
        }

        /// <summary>
        /// Verifies that the architecture obtained from updating an msix does not come from the url string.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateMsixIgnoresArchitectureFromUrl()
        {
            TestUtils.InitializeMockDownloads("arm64/" + TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MsixArchitectureMatch", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            foreach (var updatedInstaller in updatedManifests.InstallerManifest.Installers)
            {
                Assert.AreNotEqual(Architecture.Arm64, updatedInstaller.Architecture, "Architecture should not be detected from string.");
                Assert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
            }
        }

        /// <summary>
        /// Verifies that the update command blocks the submission of a manifest if no installer hash changes are detected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task BlockUpdateSubmissionsWithNoUpdate()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            (UpdateCommand command, var manifests) = GetUpdateCommandAndManifestData("TestPublisher.NoUpdate", "1.2.3.4", this.tempPath, null);
            command.SubmitToGitHub = true;
            await command.ExecuteManifestUpdate(manifests, this.testCommandEvent);
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.NoChangeDetectedInUpdatedManifest_Message), "Failed to block manifests without updates from submitting.");
        }

        /// <summary>
        /// Verfies that an error message is shown if the overriding architecture is invalid.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithArchitectureOverrideFailsParsing()
        {
            string invalidArch = "fakeArch";
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|{invalidArch}" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed due to invalid architecture specified for override.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.UnableToParseOverride_Error, invalidArch)), "Failed to show architecture override parsing error.");
        }

        /// <summary>
        /// Verfies that an error message is shown if multiple architectures are specified for an override.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFailsOverrideWithMultipleArchitectures()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|x86|ARM" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed due to multiple architecture overrides specified for a single installer.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleArchitectureOverride_Error), "Failed to show multiple architecture overrides error.");
        }

        /// <summary>
        /// Verfies that an error message is shown if multiple architectures are specified for an override.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFailsOverrideWithMultipleScopes()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|user|machine" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed due to multiple scope overrides specified for a single installer.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleScopeOverride_Error), "Failed to show multiple scope overrides error.");
        }

        /// <summary>
        /// Verifies that the overriding architecture can be matched to the architecture specified in the existing manifest and the update succeeds.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithArchitectureOverrides()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            Architecture expectedArch = Architecture.Arm;

            // Test without architecture override should fail.
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { testInstallerUrl });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed without architecture override as the installer is x64");

            // Test with architecture override should pass.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|{expectedArch}" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded as installer should be overrided with ARM architecture.");

            var updatedInstaller = updatedManifests.InstallerManifest.Installers.Single();
            Assert.AreEqual(expectedArch, updatedInstaller.Architecture, $"Architecture should be {expectedArch} from override.");
            Assert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that the overriding scope can be matched to the scope specified in the existing manifest and the update succeeds.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithScopeOverrides()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";

            // Test without scope override should fail.
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { testInstallerUrl, testInstallerUrl });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed without scope override as there are multiple installers with the same architecture.");

            // Test with scope override should pass.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|user", $"{testInstallerUrl}|machine" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded as installers should be overrided with scope.");

            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];

            Assert.AreEqual(Scope.User, updatedFirstInstaller.Scope, $"Scope should be preserved.");
            Assert.AreEqual(Scope.Machine, updatedSecondInstaller.Scope, $"Scope should be preserved.");

            Assert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            Assert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that the overriding both the architecture and scope is supported and the update succeeds.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithArchAndScopeOverrides()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";

            // Test without architecture override should fail.
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchAndScopeOverride", "1.2.3.4", this.tempPath, new[] { testInstallerUrl, testInstallerUrl });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            Assert.IsNull(failedUpdateManifests, "Command should have failed without overrides");

            // Test with scope and architecture override should pass.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchAndScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|user|arm", $"{testInstallerUrl}|arm|machine" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded as installers should be overrided with architecture and scope.");

            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];

            Assert.AreEqual(Scope.User, updatedFirstInstaller.Scope, $"Scope should be preserved.");
            Assert.AreEqual(Scope.Machine, updatedSecondInstaller.Scope, $"Scope should be preserved.");
            Assert.AreEqual(Architecture.Arm, updatedFirstInstaller.Architecture, $"Architecture should be preserved.");
            Assert.AreEqual(Architecture.Arm, updatedSecondInstaller.Architecture, $"Architecture should be preserved.");

            Assert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            Assert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that using the same installerURL with multiple architecture overrides can successfully update multiple installers.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateSameInstallerWithDifferentArchitectures()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.SameInstallerDiffArch", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86" });

            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];

            Assert.AreEqual(Architecture.X64, updatedFirstInstaller.Architecture, $"Architecture should be preserved.");
            Assert.AreEqual(Architecture.X86, updatedSecondInstaller.Architecture, $"Architecture should be preserved.");

            Assert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, $"InstallerSha256 should be updated.");
            Assert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, $"InstallerSha256 should be updated.");
        }

        /// <summary>
        /// Verifies that if a matching failure occurs when trying to perform an architecture override, a warning message is displayed.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithArchitectureOverrideFailsWithErrorMessage()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|x64" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.ArchitectureOverride_Warning), "Failed to show warning for architecture override.");
        }

        /// <summary>
        /// Verifies that an error is shown if there are too many overrides specified for a given installer.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task NumberOfOverridesExceeded()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string installerUrlOverride = $"{installerUrl}|x64|user|test";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { installerUrlOverride });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.OverrideLimitExceeded_Error, installerUrlOverride)), "Failed to show warning for override limit exceeded.");
        }

        /// <summary>
        /// Tests that the provided installer url with leading and trailing spaces is trimmed prior to being updated.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithUntrimmedInstallerUrl()
        {
            string untrimmedInstallerUrl = $"  https://fakedomain.com/{TestConstants.TestExeInstaller}   ";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.SingleExe", "1.2.3.4", this.tempPath, new[] { untrimmedInstallerUrl });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            Assert.AreNotEqual(untrimmedInstallerUrl, updatedManifests.InstallerManifest.Installers.First().InstallerUrl, "InstallerUrl was not trimmed prior to update.");
        }

        /// <summary>
        /// Tests if a new installer has null values, the update does not overwrite existing values for ProductCode, PackageFamilyName, and Platform from the existing manifest.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdatePreservesExistingValues()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.SingleExe", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstallerManifest = updatedManifests.InstallerManifest;
            Assert.AreEqual("FakeProductCode", updatedInstallerManifest.ProductCode, "Existing value for ProductCode was overwritten.");
            Assert.AreEqual("FakePackageFamilyName", updatedInstallerManifest.PackageFamilyName, "Existing value for PackageFamilyName was overwritten.");
            Assert.IsNotNull(updatedInstallerManifest.Platform, "Existing value for Platform was overwritten.;");
        }

        /// <summary>
        /// Tests when an update is unable to find an installerType match.
        /// The matching logic must resort to using a compatible installerType to determine a match (i.e. appx -> msix).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithCompatibleInstallerType()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MatchWithCompatibleInstallerType", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            Assert.AreEqual(InstallerType.Appx, updatedManifests.InstallerManifest.InstallerType, "Msix installerType should be matched with Appx");
        }

        /// <summary>
        /// Tests that the manifest version gets updated to the latest manifest schema version.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdatesToLatestManifestVersion()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.SingleExe", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            string initialManifestVersion = initialManifests.SingletonManifest.ManifestVersion;
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.InstallerManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.VersionManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.DefaultLocaleManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
        }

        /// <summary>
        /// Tests that the manifest version gets updated to the latest manifest schema version for multifile manifests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateMultifileToLatestManifestVersion()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("Multifile.MsixTest", null, this.tempPath, null, true);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            string initialManifestVersion = initialManifests.VersionManifest.ManifestVersion;
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.InstallerManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.VersionManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.DefaultLocaleManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            Assert.AreNotEqual(initialManifestVersion, updatedManifests.LocaleManifests[0].ManifestVersion, "ManifestVersion should be updated to latest.");
        }

        /// <summary>
        /// Verifies that deserialization prioritizes alias-defined fields and can correctly assign those field values.
        /// </summary>
        [Test]
        public void UpdateDeserializesAliasDefinedFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);

            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.DeserializeAliasFields", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var singletonManifest = initialManifests.SingletonManifest;
            var installer = singletonManifest.Installers.First();
            var agreement = singletonManifest.Agreements.First();

            // Verify that fields with an alias-defined counterpart are not deserialized.
            Assert.IsNull(singletonManifest.ReleaseDate, "ReleaseDate should not be defined as it is a DateTimeOffset type.");
            Assert.IsNull(installer.ReleaseDate, "The installer ReleaseDate should not be defined as it is a DateTimeOffset type.");
            Assert.IsNull(singletonManifest.Agreements.First().Agreement1, "Agreement1 should not be defined since the property name is generated incorrectly.");

            // Verify that the alias-defined fields are correctly deserialized.
            Assert.AreEqual("1-1-2022", singletonManifest.ReleaseDateTime, "The value for releaseDate should be 1-1-2022");
            Assert.AreEqual("1-1-2022", installer.ReleaseDateTime, "The value for releaseDate should be 1-1-2022");
            Assert.AreEqual("Agreement text", agreement.AgreementContent, "The value for AgreementContent should be 'Agreement text'.");
            Assert.AreEqual("Agreement label", agreement.AgreementLabel, "The value for AgreementLabel should be 'Agreement label'.");
            Assert.AreEqual("https://fakeagreementurl.com", agreement.AgreementUrl, "The value for AgreementUrl should be 'https://fakeagreementurl.com'.");
        }

        /// <summary>
        /// Ensures that all fields from the Singleton v1.1 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullSingletonVersion1_1()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullSingleton1_1", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the Singleton v1.2 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullSingletonVersion1_2()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullSingleton1_2", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the Singleton v1.4 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullSingletonVersion1_4()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullSingleton1_4", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the Singleton v1.5 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullSingletonVersion1_5()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullSingleton1_5", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that version specific fields are reset after an update.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateResetsVersionSpecificFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullSingleton1_1", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            DefaultLocaleManifest updatedDefaultLocaleManifest = updatedManifests.DefaultLocaleManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            Assert.IsNull(updatedInstaller.ReleaseDateTime, "ReleaseDate should be null.");
            Assert.IsNull(updatedInstallerManifest.ReleaseDateTime, "ReleaseDate should be null.");
            Assert.IsNull(updatedDefaultLocaleManifest.ReleaseNotes, "ReleaseNotes should be null.");
            Assert.IsNull(updatedDefaultLocaleManifest.ReleaseNotesUrl, "ReleaseNotesUrl should be null.");
        }

        /// <summary>
        /// Ensures that updating a portable package preserves the portable installerType.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdatePortable()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.Portable", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Portable, "InstallerType should be portable");
            Assert.IsTrue(updatedInstallerManifest.Commands[0] == "portableCommand", "Command value should be preserved.");
        }

        /// <summary>
        /// Ensures that updating a zip package containing an single exe installer works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateZipWithExe()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipWithExe", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType should be EXE");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var initialNestedInstallerFile = initialInstaller.NestedInstallerFiles.First();

            var updatedNestedInstallerFile = updatedInstallerManifest.NestedInstallerFiles.First();
            Assert.IsTrue(initialNestedInstallerFile.RelativeFilePath == updatedNestedInstallerFile.RelativeFilePath, "RelativeFilePath should be preserved.");
            Assert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Ensures that updating a zip package containing an single portable installer works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateZipWithPortable()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipWithPortable", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Portable, "NestedInstallerType should be PORTABLE");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var initialNestedInstallerFile = initialInstaller.NestedInstallerFiles.First();

            var updatedNestedInstallerFile = updatedInstallerManifest.NestedInstallerFiles.First();
            Assert.IsTrue(initialNestedInstallerFile.RelativeFilePath == updatedNestedInstallerFile.RelativeFilePath, "RelativeFilePath should be preserved.");
            Assert.IsTrue(initialNestedInstallerFile.PortableCommandAlias == updatedNestedInstallerFile.PortableCommandAlias, "PortableCommandAlias should be preserved.");
            Assert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Ensures that updating a zip package containing an single msi installer works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateZipWithMsi()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipWithMsi", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Msi, "NestedInstallerType should be MSI");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();

            Assert.IsTrue(initialInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedInstallerManifest.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            Assert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
            Assert.IsNotNull(updatedInstallerManifest.ProductCode, "ProductCode should be updated.");
        }

        /// <summary>
        /// Ensures that updating a zip package containing an single msix installer works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateZipWithMsix()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipWithMsix", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedFirstInstaller = updatedInstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedInstallerManifest.Installers[1];

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Msix, "NestedInstallerType should be MSIX");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstallers = initialManifests.SingletonManifest.Installers;
            var initialFirstInstaller = initialInstallers[0];
            var initialSecondInstaller = initialInstallers[1];

            Assert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedInstallerManifest.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");

            Assert.IsNotNull(updatedFirstInstaller.SignatureSha256, "SignatureSha256 should be updated.");
            Assert.IsNotNull(updatedSecondInstaller.SignatureSha256, "SignatureSha256 should be updated.");

            Assert.IsTrue(initialFirstInstaller.InstallerSha256 != updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            Assert.IsTrue(initialSecondInstaller.InstallerSha256 != updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that moving common installer fields to the root of the manifest works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task MoveInstallerFieldsToRoot()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MoveInstallerFieldsToRoot", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86", $"{installerUrl}|arm" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType at the root level should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType at the root level should be EXE");
            Assert.IsTrue(updatedInstallerManifest.Scope == Scope.Machine, "Scope at the root level should be machine");
            Assert.IsTrue(updatedInstallerManifest.MinimumOSVersion == "10.0.22000.0", "MinimumOSVersion at the root level should be 10.0.22000.0");
            Assert.IsTrue(updatedInstallerManifest.PackageFamilyName == "TestPackageFamilyName", "PackageFamilyName at the root level should be TestPackageFamilyName");
            Assert.IsTrue(updatedInstallerManifest.UpgradeBehavior == UpgradeBehavior.Install, "UpgradeBehavior at the root level should be install");
            Assert.IsTrue(updatedInstallerManifest.ElevationRequirement == ElevationRequirement.ElevationRequired, "ElevationRequirement at the root level should be elevationRequired");
            Assert.IsTrue(updatedInstallerManifest.InstallerAbortsTerminal == true, "InstallerAbortsTerminal at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.InstallLocationRequired == true, "InstallLocation at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.RequireExplicitUpgrade == true, "RequireExplicitUpgrade at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.DisplayInstallWarnings == true, "DisplayInstallWarnings at the root level should be true");
            Assert.IsNotNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.Platform, "Platform at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.Commands, "Commands at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.Protocols, "Protocols at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should not be null");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // Assert.IsNotNull(updatedInstallerManifest.Markets, "Markets at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should not be null");
            foreach (var installer in updatedInstallerManifest.Installers)
            {
                Assert.IsNull(installer.InstallerType, "InstallerType at the installer level should be null");
                Assert.IsNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should be null");
                Assert.IsNull(installer.Scope, "Scope at the installer level should be null");
                Assert.IsNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should be null");
                Assert.IsNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should be null");
                Assert.IsNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should be null");
                Assert.IsNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should be null");
                Assert.IsNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should be null");
                Assert.IsNull(installer.InstallLocationRequired, "InstallLocation at the installer level should be null");
                Assert.IsNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should be null");
                Assert.IsNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should be null");
                Assert.IsNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should be null");
                Assert.IsNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should be null");
                Assert.IsNull(installer.Dependencies, "Dependencies at the installer level should be null");
                Assert.IsNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should be null");
                Assert.IsNull(installer.Platform, "Platform at the installer level should be null");
                Assert.IsNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should be null");
                Assert.IsNull(installer.Commands, "Commands at the installer level should be null");
                Assert.IsNull(installer.Protocols, "Protocols at the installer level should be null");
                Assert.IsNull(installer.FileExtensions, "FileExtensions at the installer level should be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // Assert.IsNull(installer.Markets, "Markets at the installer level should be null");
                Assert.IsNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should be null");
                Assert.IsNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should be null");
                Assert.IsNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should be null");
                Assert.IsNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should be null");
            }
        }

        /// <summary>
        /// Verifies that properties that are not common to all installers are retained at the installer level.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task DontMoveInstallerFieldsToRoot()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller, TestConstants.TestExeInstaller);
            string installerUrlZip = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            string installerUrlExe = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.DontMoveInstallerFieldsToRoot", null, this.tempPath, new[] { $"{installerUrlExe}", $"{installerUrlZip}|x86", $"{installerUrlZip}|arm" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            Assert.IsNull(updatedInstallerManifest.InstallerType, "InstallerType at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.NestedInstallerType, "NestedInstallerType at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.Scope, "Scope at the root level should not be null");
            Assert.IsNull(updatedInstallerManifest.MinimumOSVersion, "MinimumOSVersion at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.PackageFamilyName, "PackageFamilyName at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.UpgradeBehavior, "UpgradeBehavior at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.ElevationRequirement, "ElevationRequirement at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.InstallerAbortsTerminal, "InstallerAbortsTerminal at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.InstallLocationRequired, "InstallLocation at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.RequireExplicitUpgrade, "RequireExplicitUpgrade at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.DisplayInstallWarnings, "DisplayInstallWarnings at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.Platform, "Platform at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.Commands, "Commands at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.Protocols, "Protocols at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should be null");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // Assert.IsNull(updatedInstallerManifest.Markets, "Markets at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should be null");
            Assert.IsNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should be null");

            foreach (var installer in updatedInstallerManifest.Installers)
            {
                Assert.IsNotNull(installer.InstallerType, "InstallerType at the installer level should not be null");
                Assert.IsNotNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should not be null");
                Assert.IsNotNull(installer.Scope, "Scope at the installer level should not be null");
                Assert.IsNotNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should not be null");
                Assert.IsNotNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should not be null");
                Assert.IsNotNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should not be null");
                Assert.IsNotNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should not be null");
                Assert.IsNotNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should not be null");
                Assert.IsNotNull(installer.InstallLocationRequired, "InstallLocation at the installer level should not be null");
                Assert.IsNotNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should not be null");
                Assert.IsNotNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should not be null");
                Assert.IsNotNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should not be null");
                Assert.IsNotNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should not be null");
                Assert.IsNotNull(installer.Dependencies, "Dependencies at the installer level should not be null");
                Assert.IsNotNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should not be null");
                Assert.IsNotNull(installer.Platform, "Platform at the installer level should not be null");
                Assert.IsNotNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should not be null");
                Assert.IsNotNull(installer.Commands, "Commands at the installer level should not be null");
                Assert.IsNotNull(installer.Protocols, "Protocols at the installer level should not be null");
                Assert.IsNotNull(installer.FileExtensions, "FileExtensions at the installer level should not be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // Assert.IsNotNull(installer.Markets, "Markets at the installer level should not be null");
                Assert.IsNotNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should not be null");
                Assert.IsNotNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should not be null");
                Assert.IsNotNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should not be null");
                Assert.IsNotNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should not be null");
            }
        }

        /// <summary>
        /// Verifies that installer fields are overridden by root fields in update scenario.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateOverridesInstallerFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.OverrideInstallerFields", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            Assert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType at the root level should be ZIP");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType at the root level should be EXE");
            Assert.IsTrue(updatedInstallerManifest.Scope == Scope.Machine, "Scope at the root level should be machine");
            Assert.IsTrue(updatedInstallerManifest.MinimumOSVersion == "10.0.22000.0", "MinimumOSVersion at the root level should be 10.0.22000.0");
            Assert.IsTrue(updatedInstallerManifest.PackageFamilyName == "TestPackageFamilyName1", "PackageFamilyName at the root level should be TestPackageFamilyName");
            Assert.IsTrue(updatedInstallerManifest.UpgradeBehavior == UpgradeBehavior.Install, "UpgradeBehavior at the root level should be install");
            Assert.IsTrue(updatedInstallerManifest.ElevationRequirement == ElevationRequirement.ElevationRequired, "ElevationRequirement at the root level should be elevationRequired");
            Assert.IsTrue(updatedInstallerManifest.InstallerAbortsTerminal == true, "InstallerAbortsTerminal at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.InstallLocationRequired == true, "InstallLocation at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.RequireExplicitUpgrade == true, "RequireExplicitUpgrade at the root level should be true");
            Assert.IsTrue(updatedInstallerManifest.DisplayInstallWarnings == true, "DisplayInstallWarnings at the root level should be true");
            Assert.IsNotNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.NestedInstallerFiles[0].PortableCommandAlias == "PortableCommandAlias1", "PortableCommandAlias at the root level should be PortableCommandAlias1");
            Assert.IsNotNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.InstallerSwitches.Silent == "/silent1", "Silent installer switch at the root level should be /silent1");
            Assert.IsNotNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.Dependencies.PackageDependencies[0].PackageIdentifier == "TestPackageDependency1", "PackageDependencies PackageIdentifier at the root level should be TestPackageDependency1");
            Assert.IsNotNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.AppsAndFeaturesEntries[0].ProductCode == "TestProductCode1", "AppsAndFeaturesEntries ProductCode at the root level should be TestProduct1");
            Assert.IsNotNull(updatedInstallerManifest.Platform, "Platform at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.Platform[0] == Platform.Windows_Desktop, "Platform at the root level should contain Windows.Desktop");
            Assert.IsNotNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.ExpectedReturnCodes[0].InstallerReturnCode == 1001, "ExpectedReturnCodes InstallerReturnCode at the root level should be 1001");
            Assert.IsNotNull(updatedInstallerManifest.Commands, "Commands at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.Commands[0] == "fakeCommand1", "Commands at the root level should contain fakeCommand1");
            Assert.IsNotNull(updatedInstallerManifest.Protocols, "Protocols at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.Protocols[0] == "fakeProtocol1", "Protocols at the root level should contain fakeProtocol1");
            Assert.IsNotNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.FileExtensions[0] == ".exe", "FileExtensions at the root level should contain .exe");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // Assert.IsNotNull(updatedInstallerManifest.Markets, "Markets at the root level should not be null");
            Assert.IsNotNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.UnsupportedOSArchitectures[0] == UnsupportedOSArchitecture.Arm64, "UnsupportedOSArchitectures at the root level should contain arm64");
            Assert.IsNotNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.InstallerSuccessCodes[0] == 1, "InstallerSuccessCodes at the root level should contain 1");
            Assert.IsNotNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.UnsupportedArguments[1] == UnsupportedArgument.Location, "UnsupportedArguments at the root level should contain location");
            Assert.IsNotNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should not be null");
            Assert.IsTrue(updatedInstallerManifest.InstallationMetadata.DefaultInstallLocation == "%ProgramFiles%\\TestApp1", "DefaultInstallLocation at the root level should be ProgramFiles/TestApp1");
            foreach (var installer in updatedInstallerManifest.Installers)
            {
                Assert.IsNull(installer.InstallerType, "InstallerType at the installer level should be null");
                Assert.IsNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should be null");
                Assert.IsNull(installer.Scope, "Scope at the installer level should be null");
                Assert.IsNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should be null");
                Assert.IsNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should be null");
                Assert.IsNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should be null");
                Assert.IsNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should be null");
                Assert.IsNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should be null");
                Assert.IsNull(installer.InstallLocationRequired, "InstallLocation at the installer level should be null");
                Assert.IsNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should be null");
                Assert.IsNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should be null");
                Assert.IsNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should be null");
                Assert.IsNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should be null");
                Assert.IsNull(installer.Dependencies, "Dependencies at the installer level should be null");
                Assert.IsNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should be null");
                Assert.IsNull(installer.Platform, "Platform at the installer level should be null");
                Assert.IsNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should be null");
                Assert.IsNull(installer.Commands, "Commands at the installer level should be null");
                Assert.IsNull(installer.Protocols, "Protocols at the installer level should be null");
                Assert.IsNull(installer.FileExtensions, "FileExtensions at the installer level should be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // Assert.IsNull(installer.Markets, "Markets at the installer level should be null");
                Assert.IsNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should be null");
                Assert.IsNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should be null");
                Assert.IsNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should be null");
                Assert.IsNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should be null");
            }
        }

        /// <summary>
        /// Verifies that the appropriate error message is displayed if the nested installer is not found.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateZipInvalidRelativeFilePath()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.InvalidRelativeFilePath", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.NestedInstallerFileNotFound_Error, "fakeRelativeFilePath.exe")), "Failed to show warning for invalid relative file path.");
        }

        private static (UpdateCommand UpdateCommand, List<string> InitialManifestContent) GetUpdateCommandAndManifestData(string id, string version, string outputDir, IEnumerable<string> installerUrls, bool isMultifile = false)
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

            var initialManifestContent = isMultifile ? TestUtils.GetInitialMultifileManifestContent(id) : TestUtils.GetInitialManifestContent($"{id}.yaml");
            return (updateCommand, initialManifestContent);
        }

        private static async Task<Manifests> RunUpdateCommand(UpdateCommand updateCommand, List<string> initialManifestContent)
        {
            Manifests initialManifests = updateCommand.DeserializeManifestContentAndApplyInitialUpdate(initialManifestContent);
            return await updateCommand.UpdateManifestsAutonomously(initialManifests);
        }
    }
}
