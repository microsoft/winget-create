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
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Serializers;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

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
            Serialization.ManifestSerializer = new YamlSerializer();
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
        public async Task UpdateAndVerifyManifestsCreated()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);

            string packageIdentifier = "TestPublisher.SingleExe";
            string version = "1.2.3.4";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(packageIdentifier, version, this.tempPath, new[] { $"{installerUrl}" });
            var updatedManifests = await command.ExecuteManifestUpdate(initialManifestContent, this.testCommandEvent);
            ClassicAssert.IsTrue(updatedManifests, "Command should have succeeded");

            string manifestDir = Utils.GetAppManifestDirPath(packageIdentifier, version);
            var updatedManifestContents = Directory.GetFiles(Path.Combine(this.tempPath, manifestDir)).Select(f => File.ReadAllText(f));
            ClassicAssert.IsTrue(updatedManifestContents.Any(), "Updated manifests were not created successfully");
            Manifests manifestsToValidate = Serialization.DeserializeManifestContents(updatedManifestContents);
            ClassicAssert.AreEqual(version, manifestsToValidate.VersionManifest.PackageVersion, $"Failed to update version of {packageIdentifier}");
        }

        /// <summary>
        /// Tests the <see cref="UpdateCommand.UpdateManifestsAutonomously"/> command, ensuring that it updates properties as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateAndVerifyUpdatedProperties()
        {
            string packageId = TestConstants.YamlConstants.TestMultifileMsixPackageIdentifier;
            string version = "1.2.3.4";
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestMsixInstaller}";
            string releaseDateString = "2024-01-01";
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            var initialManifestContent = TestUtils.GetInitialMultifileManifestContent(packageId);
            UpdateCommand command = new UpdateCommand
            {
                Id = packageId,
                Version = version,
                InstallerUrls = new[] { installerUrl },
                SubmitToGitHub = false,
                OutputDir = this.tempPath,
                ReleaseDate = DateTimeOffset.Parse(releaseDateString),
                ReleaseNotesUrl = "https://fakedomain.com/",
                Format = ManifestFormat.Yaml,
            };

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.InstallerManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstaller = updatedManifests.InstallerManifest.Installers.First();
            ClassicAssert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreEqual(releaseDateString, updatedManifests.InstallerManifest.ReleaseDateTime, "ReleaseDate should be updated");
            ClassicAssert.AreEqual(command.ReleaseNotesUrl, updatedManifests.DefaultLocaleManifest.ReleaseNotesUrl, "ReleaseNotesUrl should be updated");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            ClassicAssert.AreEqual(version, updatedManifests.VersionManifest.PackageVersion, "Version should be updated");
            ClassicAssert.AreEqual("en-US", updatedManifests.InstallerManifest.InstallerLocale, "InstallerLocale should be carried forward from existing installer root node");

            foreach (var updatedInstaller in updatedManifests.InstallerManifest.Installers)
            {
                ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");

                if (updatedInstaller.InstallerType == InstallerType.Msi || updatedInstaller.InstallerType == InstallerType.Msix)
                {
                    if (updatedInstaller.InstallerType == InstallerType.Msi)
                    {
                        ClassicAssert.AreNotEqual(initialInstaller.ProductCode, updatedInstaller.ProductCode, "ProductCode should be updated");
                        ClassicAssert.AreEqual(Scope.Machine, updatedInstaller.Scope, "Scope should be carried forward from existing installer");
                    }
                    else
                    {
                        ClassicAssert.AreNotEqual(initialInstaller.MinimumOSVersion, updatedInstaller.MinimumOSVersion, "MinimumOSVersion should be updated");
                        ClassicAssert.AreNotEqual(initialInstaller.PackageFamilyName, updatedInstaller.PackageFamilyName, "PackageFamilyName should be updated");
                        ClassicAssert.AreNotEqual(initialInstaller.Platform, updatedInstaller.Platform, "Platform should be updated");
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
            ClassicAssert.IsTrue(updateExecuted, "Command should have succeeded");
            string manifestDir = Utils.GetAppManifestDirPath(packageId, version);
            var updatedManifestContents = Directory.GetFiles(Path.Combine(this.tempPath, manifestDir)).Select(f => File.ReadAllText(f));
            ClassicAssert.IsTrue(updatedManifestContents.Any(), "Updated manifests were not created successfully");

            Manifests updatedManifests = Serialization.DeserializeManifestContents(updatedManifestContents);
            ClassicAssert.IsNull(updatedManifests.DefaultLocaleManifest.PrivacyUrl, "PrivacyUrl should be null.");
            ClassicAssert.IsNull(updatedManifests.DefaultLocaleManifest.Author, "Author should be null.");

            var firstInstaller = updatedManifests.InstallerManifest.Installers.First();
            ClassicAssert.IsNull(firstInstaller.ProductCode, "ProductCode should be null.");
            ClassicAssert.IsNull(firstInstaller.PackageFamilyName, "ProductCode should be null.");
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepancy in the URL count.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithInstallerUrlCountDiscrepancy()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData(TestConstants.TestMultipleInstallerPackageIdentifier, null, this.tempPath, new[] { "fakeurl" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleInstallerUpdateDiscrepancy_Error), "Installer discrepancy error should be thrown");
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepancy in the package count.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithPackageCountDiscrepancy()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.SingleMsixInExistingBundle", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.InstallerCountMustMatch_Error), "Installer count must match error should be thrown");
        }

        /// <summary>
        /// Verify that update command fails if there is a discrepancy in the package types.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateFailsWithUnmatchedPackages()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MismatchedMsixInExistingBundle", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.NewInstallerUrlMustMatchExisting_Message), "New installer must match error should be thrown");
        }

        /// <summary>
        /// Verify that update command warns if submit arguments are provided without submit flag being set.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateChecksMissingSubmitFlagWithReplace()
        {
            string packageId = "TestPublisher.TestPackageId";
            string version = "1.2.3.4";

            UpdateCommand command = new UpdateCommand
            {
                Id = packageId,
                Version = version,
                InstallerUrls = new[] { "https://fakedomain.com/fakeinstaller.exe" },
                SubmitToGitHub = false,
                Replace = true,
            };

            try
            {
                await command.Execute();
            }
            catch (Exception)
            {
                // Expected exception
            }

            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.SubmitFlagMissing_Warning), "Submit flag missing warning should be shown");
        }

        /// <summary>
        /// Verify that update command warns if submit arguments are provided without submit flag being set.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateChecksMissingSubmitFlagWithPRTitle()
        {
            string packageId = "TestPublisher.TestPackageId";
            string version = "1.2.3.4";

            UpdateCommand command = new UpdateCommand
            {
                Id = packageId,
                Version = version,
                InstallerUrls = new[] { "https://fakedomain.com/fakeinstaller.exe" },
                SubmitToGitHub = false,
                PRTitle = "Test PR Title",
            };

            try
            {
                await command.Execute();
            }
            catch (Exception)
            {
                // Expected exception
            }

            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.SubmitFlagMissing_Warning), "Submit flag missing warning should be shown");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            foreach (var item in expectedArchs.Zip(updatedManifests.InstallerManifest.Installers, (expectedArch, installer) => (expectedArch, installer)))
            {
                ClassicAssert.AreEqual(item.expectedArch, item.installer.Architecture, "Architecture not parsed correctly from url string");
                ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, item.installer.InstallerSha256, "InstallerSha256 should be updated");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            foreach (var updatedInstaller in updatedManifests.InstallerManifest.Installers)
            {
                ClassicAssert.AreNotEqual(Architecture.Arm64, updatedInstaller.Architecture, "Architecture should not be detected from string.");
                ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
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
        /// Verifies that an error message is shown if multiple architectures are specified for an override.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFailsOverrideWithMultipleArchitectures()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|x86|ARM" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed due to multiple architecture overrides specified for a single installer.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleArchitectureOverride_Error), "Failed to show multiple architecture overrides error.");
        }

        /// <summary>
        /// Verifies that an error message is shown if multiple architectures are specified for an override.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFailsOverrideWithMultipleScopes()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|user|machine" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed due to multiple scope overrides specified for a single installer.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.MultipleScopeOverride_Error), "Failed to show multiple scope overrides error.");
        }

        /// <summary>
        /// Verifies that an error message is shown if multiple architectures are specified for an override.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFailsWithMultipleDisplayVersions()
        {
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string displayVersion1 = "3.4";
            string displayVersion2 = "1.2";
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{installerUrl}|{displayVersion1}|{displayVersion2}" });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed due to multiple display versions specified for a single installer.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.UnableToParseArgument_Error, displayVersion2)), "Failed to show parsing error due to multiple string overrides.");
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
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed without architecture override as the installer is x64");

            // Test with architecture override should pass.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|{expectedArch}" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded as installer should be overrided with ARM architecture.");

            var updatedInstaller = updatedManifests.InstallerManifest.Installers.Single();
            ClassicAssert.AreEqual(expectedArch, updatedInstaller.Architecture, $"Architecture should be {expectedArch} from override.");
            ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
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
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed without scope override as there are multiple installers with the same architecture.");

            // Test with scope override should pass.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ScopeOverride", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|user", $"{testInstallerUrl}|machine" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded as installers should be overrided with scope.");

            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];

            ClassicAssert.AreEqual(Scope.User, updatedFirstInstaller.Scope, $"Scope should be preserved.");
            ClassicAssert.AreEqual(Scope.Machine, updatedSecondInstaller.Scope, $"Scope should be preserved.");

            ClassicAssert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that the providing all supported URL arguments will result in a successful update.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithAllUrlArguments()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string newDisplayVersion1 = "2.3";
            string newDisplayVersion2 = "4.5";

            // Test without architecture override should fail.
            (UpdateCommand badCommand, var manifests) =
                GetUpdateCommandAndManifestData("TestPublisher.AllUrlArguments", "1.2.3.4", this.tempPath, new[] { testInstallerUrl, testInstallerUrl });
            var failedUpdateManifests = await RunUpdateCommand(badCommand, manifests);
            ClassicAssert.IsNull(failedUpdateManifests, "Command should have failed without overrides");

            // Test with scope and architecture override should pass. DisplayVersion should also be updated.
            (UpdateCommand goodCommand, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.AllUrlArguments", "1.2.3.4", this.tempPath, new[] { $"{testInstallerUrl}|user|arm|{newDisplayVersion1}", $"{testInstallerUrl}|arm|machine|{newDisplayVersion2}" });
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(goodCommand, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded as installers should be overrided with architecture and scope.");

            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];
            var initialFirstDisplayVersion = initialFirstInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var initialSecondDisplayVersion = initialSecondInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];
            var updatedFirstDisplayVersion = updatedFirstInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var updatedSecondDisplayVersion = updatedSecondInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            ClassicAssert.AreEqual(Scope.User, updatedFirstInstaller.Scope, $"Scope should be preserved.");
            ClassicAssert.AreEqual(Scope.Machine, updatedSecondInstaller.Scope, $"Scope should be preserved.");
            ClassicAssert.AreEqual(Architecture.Arm, updatedFirstInstaller.Architecture, $"Architecture should be preserved.");
            ClassicAssert.AreEqual(Architecture.Arm, updatedSecondInstaller.Architecture, $"Architecture should be preserved.");
            ClassicAssert.AreEqual(newDisplayVersion1, updatedFirstDisplayVersion, $"DisplayVersion should be updated.");
            ClassicAssert.AreEqual(newDisplayVersion2, updatedSecondDisplayVersion, $"DisplayVersion should be updated.");

            ClassicAssert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialFirstDisplayVersion, updatedFirstDisplayVersion, "DisplayVersion should be updated");
            ClassicAssert.AreNotEqual(initialSecondDisplayVersion, updatedSecondDisplayVersion, "DisplayVersion should be updated");
        }

        /// <summary>
        /// Verifies that display version provided as CLI arg and in the URL arguments correctly updates the display version in the manifest.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateDisplayVersion()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string displayVersionForCLIArg = "2.3";
            string newDisplayVersionForUrl1 = "4.5";
            string newDisplayVersionForUrl2 = "6.7";

            var initialManifestContent = TestUtils.GetInitialManifestContent("TestPublisher.UpdateDisplayVersion.yaml");
            UpdateCommand command = new UpdateCommand
            {
                Id = "TestPublisher.UpdateDisplayVersion",
                Version = "1.2.3.4",
                InstallerUrls = new[]
                {
                    $"{testInstallerUrl}|x64|{newDisplayVersionForUrl1}",
                    $"{testInstallerUrl}|x86|{newDisplayVersionForUrl2}",
                    $"{testInstallerUrl}|arm",
                    $"{testInstallerUrl}|arm64",
                },
                DisplayVersion = displayVersionForCLIArg,
            };
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded.");

            // Initial installers
            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];
            var initialThirdInstaller = initialManifests.SingletonManifest.Installers[2];
            var initialFourthInstaller = initialManifests.SingletonManifest.Installers[3];

            // Initial display versions (fourth installer does not have a display version)
            var initialFirstDisplayVersion = initialFirstInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var initialSecondDisplayVersion = initialSecondInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var initialThirdDisplayVersion = initialThirdInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            // Updated installers
            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];
            var updatedThirdInstaller = updatedManifests.InstallerManifest.Installers[2];
            var updatedFourthInstaller = updatedManifests.InstallerManifest.Installers[3];

            // Updated display versions (fourth installer does not have a display version)
            var updatedFirstDisplayVersion = updatedFirstInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var updatedSecondDisplayVersion = updatedSecondInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;
            var updatedThirdDisplayVersion = updatedThirdInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            string result = this.sw.ToString();
            Assert.That(result, Does.Not.Contain(Resources.UnchangedDisplayVersion_Warning), "Unchanged display version warning should not be shown.");
            Assert.That(result, Does.Not.Contain(Resources.InstallerWithMultipleDisplayVersions_Warning), "Single installer with multiple display versions warning should not be shown.");

            ClassicAssert.AreEqual(newDisplayVersionForUrl1, updatedFirstDisplayVersion, $"DisplayVersion should be updated by the value in the URL argument.");
            ClassicAssert.AreEqual(newDisplayVersionForUrl2, updatedSecondDisplayVersion, $"DisplayVersion should be updated by the value in the URL argument.");
            ClassicAssert.AreEqual(displayVersionForCLIArg, updatedThirdDisplayVersion, $"DisplayVersion should be updated by the value in the CLI arg");
            ClassicAssert.IsNull(updatedFourthInstaller.AppsAndFeaturesEntries);

            ClassicAssert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialThirdInstaller.InstallerSha256, updatedThirdInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialFourthInstaller.InstallerSha256, updatedFourthInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreNotEqual(initialFirstDisplayVersion, updatedFirstDisplayVersion, "DisplayVersion should be updated");
            ClassicAssert.AreNotEqual(initialSecondDisplayVersion, updatedSecondDisplayVersion, "DisplayVersion should be updated");
            ClassicAssert.AreNotEqual(updatedThirdDisplayVersion, initialThirdDisplayVersion, "DisplayVersion should be updated");
        }

        /// <summary>
        /// Verifies that update commands shows a warning if a single installer has multiple display versions.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateShowsWarningForSingleInstallerWithMultipleDisplayVersions()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string newDisplayVersionForUrl1 = "4.5";
            string newDisplayVersionForUrl2 = "6.7";

            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.SingleInstallerWithMultipleDisplayVersions", null, this.tempPath, new[]
                {
                    $"{testInstallerUrl}|x64|{newDisplayVersionForUrl1}",
                    $"{testInstallerUrl}|x86|{newDisplayVersionForUrl2}",
                });

            var originalManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var originalInstallers = originalManifests.SingletonManifest.Installers;
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded.");

            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.InstallerWithMultipleDisplayVersions_Warning), "Single installer with multiple display versions warning should be shown.");

            // Get original display versions for a single installer
            var originalFirstDisplayVersion = originalInstallers[0].AppsAndFeaturesEntries[0].DisplayVersion;
            var originalSecondDisplayVersion = originalInstallers[0].AppsAndFeaturesEntries[1].DisplayVersion;
            var originalThirdDisplayVersion = originalInstallers[0].AppsAndFeaturesEntries[2].DisplayVersion;

            var updatedInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedFirstDisplayVersion = updatedInstaller.AppsAndFeaturesEntries[0].DisplayVersion;
            var updatedSecondDisplayVersion = updatedInstaller.AppsAndFeaturesEntries[1].DisplayVersion;
            var updatedThirdDisplayVersion = updatedInstaller.AppsAndFeaturesEntries[2].DisplayVersion;

            // Winget-Create should only update the display version for the first entry and leave the rest unchanged.
            ClassicAssert.AreEqual(newDisplayVersionForUrl1, updatedFirstDisplayVersion, "DisplayVersion should be updated by the value in the URL argument.");
            ClassicAssert.AreEqual(originalSecondDisplayVersion, updatedSecondDisplayVersion, "DisplayVersion should remain same.");
            ClassicAssert.AreEqual(originalThirdDisplayVersion, updatedThirdDisplayVersion, "DisplayVersion should remain same.");
        }

        /// <summary>
        /// Verifies that update commands shows a warning if the display version is unchanged for an installer.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateShowsWarningForUnchangedDisplayVersion()
        {
            TestUtils.InitializeMockDownload();
            TestUtils.SetMockHttpResponseContent(TestConstants.TestExeInstaller);
            string testInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string newDisplayVersionForUrl1 = "4.5";
            string newDisplayVersionForUrl2 = "6.7";

            var initialManifestContent = TestUtils.GetInitialManifestContent("TestPublisher.UpdateDisplayVersion.yaml");
            UpdateCommand command = new UpdateCommand
            {
                Id = "TestPublisher.UpdateDisplayVersion",
                Version = "1.2.3.4",
                InstallerUrls = new[]
                {
                    $"{testInstallerUrl}|x64|{newDisplayVersionForUrl1}",
                    $"{testInstallerUrl}|x86|{newDisplayVersionForUrl2}",
                    $"{testInstallerUrl}|arm",
                    $"{testInstallerUrl}|arm64",
                },
            };
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded.");

            var initialThirdInstaller = initialManifests.SingletonManifest.Installers[2];
            var initialThirdDisplayVersion = initialThirdInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            var updatedThirdInstaller = updatedManifests.InstallerManifest.Installers[2];
            var updatedThirdDisplayVersion = updatedThirdInstaller.AppsAndFeaturesEntries.FirstOrDefault().DisplayVersion;

            // DisplayVersion unchanged for third installer
            ClassicAssert.AreEqual(initialThirdDisplayVersion, updatedThirdDisplayVersion, $"DisplayVersion should remain same.");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(Resources.UnchangedDisplayVersion_Warning), "Unchanged display version warning should be shown.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialFirstInstaller = initialManifests.SingletonManifest.Installers[0];
            var initialSecondInstaller = initialManifests.SingletonManifest.Installers[1];

            var updatedFirstInstaller = updatedManifests.InstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedManifests.InstallerManifest.Installers[1];

            ClassicAssert.AreEqual(Architecture.X64, updatedFirstInstaller.Architecture, $"Architecture should be preserved.");
            ClassicAssert.AreEqual(Architecture.X86, updatedSecondInstaller.Architecture, $"Architecture should be preserved.");

            ClassicAssert.AreNotEqual(initialFirstInstaller.InstallerSha256, updatedFirstInstaller.InstallerSha256, $"InstallerSha256 should be updated.");
            ClassicAssert.AreNotEqual(initialSecondInstaller.InstallerSha256, updatedSecondInstaller.InstallerSha256, $"InstallerSha256 should be updated.");
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
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
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
            string installerUrlOverride = $"{installerUrl}|x64|user|1.3.4|test";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.ArchitectureOverride", "1.2.3.4", this.tempPath, new[] { installerUrlOverride });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.ArgumentLimitExceeded_Error, installerUrlOverride)), "Failed to show error for argument limit exceeded.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            ClassicAssert.AreNotEqual(untrimmedInstallerUrl, updatedManifests.InstallerManifest.Installers.First().InstallerUrl, "InstallerUrl was not trimmed prior to update.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            var updatedInstallerManifest = updatedManifests.InstallerManifest;
            ClassicAssert.AreEqual("FakeProductCode", updatedInstallerManifest.ProductCode, "Existing value for ProductCode was overwritten.");
            ClassicAssert.AreEqual("Fake.PackageFamilyName_8wekyb3d8bbwe", updatedInstallerManifest.PackageFamilyName, "Existing value for PackageFamilyName was overwritten.");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Platform, "Existing value for Platform was overwritten.;");
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
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            ClassicAssert.AreEqual(InstallerType.Appx, updatedManifests.InstallerManifest.InstallerType, "Msix installerType should be matched with Appx");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.InstallerManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.VersionManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.DefaultLocaleManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
        }

        /// <summary>
        /// Tests that the manifest version gets updated to the latest manifest schema version for multifile manifests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateMultifileToLatestManifestVersion()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestMsixInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("Multifile.Yaml.MsixTest", null, this.tempPath, null, true);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            string initialManifestVersion = initialManifests.VersionManifest.ManifestVersion;
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.InstallerManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.VersionManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.DefaultLocaleManifest.ManifestVersion, "ManifestVersion should be updated to latest.");
            ClassicAssert.AreNotEqual(initialManifestVersion, updatedManifests.LocaleManifests[0].ManifestVersion, "ManifestVersion should be updated to latest.");
        }

        /// <summary>
        /// Verifies that deserialization prioritizes alias-defined fields and can correctly assign those field values.
        /// </summary>
        [Test]
        public void UpdateDeserializesAliasDefinedFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (_, List<string> initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.DeserializeAliasFields", null, this.tempPath, null);
            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var singletonManifest = initialManifests.SingletonManifest;
            var installer = singletonManifest.Installers.First();
            var agreement = singletonManifest.Agreements.First();

            // Verify that fields with an alias-defined counterpart are not deserialized.
            ClassicAssert.IsNull(singletonManifest.ReleaseDate, "ReleaseDate should not be defined as it is a DateTimeOffset type.");
            ClassicAssert.IsNull(installer.ReleaseDate, "The installer ReleaseDate should not be defined as it is a DateTimeOffset type.");
            ClassicAssert.IsNull(singletonManifest.Agreements.First().Agreement1, "Agreement1 should not be defined since the property name is generated incorrectly.");

            // Verify that the alias-defined fields are correctly deserialized.
            ClassicAssert.AreEqual("1-1-2022", singletonManifest.ReleaseDateTime, "The value for releaseDate should be 1-1-2022");
            ClassicAssert.AreEqual("1-1-2022", installer.ReleaseDateTime, "The value for releaseDate should be 1-1-2022");
            ClassicAssert.AreEqual("Agreement text", agreement.AgreementContent, "The value for AgreementContent should be 'Agreement text'.");
            ClassicAssert.AreEqual("Agreement label", agreement.AgreementLabel, "The value for AgreementLabel should be 'Agreement label'.");
            ClassicAssert.AreEqual("https://fakeagreementurl.com", agreement.AgreementUrl, "The value for AgreementUrl should be 'https://fakeagreementurl.com'.");
        }

        /// <summary>
        /// Ensures that all fields from the YAML Singleton v1.1 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullYamlSingletonVersion1_1()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullYamlSingleton1_1", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the YAML Singleton v1.2 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullYamlSingletonVersion1_2()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullYamlSingleton1_2", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the YAML Singleton v1.4 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullYamlSingletonVersion1_4()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullYamlSingleton1_4", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the YAML Singleton v1.5 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullYamlSingletonVersion1_5()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullYamlSingleton1_5", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the JSON Singleton v1.1 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullJsonSingletonVersion1_1()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullJsonSingleton1_1", null, this.tempPath, null, manifestFormat: ManifestFormat.Json);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the JSON Singleton v1.2 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullJsonSingletonVersion1_2()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullJsonSingleton1_2", null, this.tempPath, null, manifestFormat: ManifestFormat.Json);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the JSON Singleton v1.4 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullJsonSingletonVersion1_4()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullJsonSingleton1_4", null, this.tempPath, null, manifestFormat: ManifestFormat.Json);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that all fields from the Singleton JSON v1.5 manifest can be deserialized and updated correctly.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateFullJsonSingletonVersion1_5()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullJsonSingleton1_5", null, this.tempPath, null, manifestFormat: ManifestFormat.Json);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");
        }

        /// <summary>
        /// Ensures that version specific fields are reset after an update when using YAML manifests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateResetsVersionSpecificFields_Yaml()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullYamlSingleton1_1", null, this.tempPath, null);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            DefaultLocaleManifest updatedDefaultLocaleManifest = updatedManifests.DefaultLocaleManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            ClassicAssert.IsNull(updatedInstaller.ReleaseDateTime, "ReleaseDateTime at installer level should be null.");
            ClassicAssert.IsNull(updatedInstaller.ReleaseDate, "ReleaseDate at installer level should be null.");
            ClassicAssert.IsNull(updatedInstallerManifest.ReleaseDateTime, "ReleaseDateTime at root level should be null.");
            ClassicAssert.IsNull(updatedInstallerManifest.ReleaseDate, "ReleaseDate at root level should be null.");
            ClassicAssert.IsNull(updatedDefaultLocaleManifest.ReleaseNotes, "ReleaseNotes should be null.");
            ClassicAssert.IsNull(updatedDefaultLocaleManifest.ReleaseNotesUrl, "ReleaseNotesUrl should be null.");
        }

        /// <summary>
        /// Ensures that version specific fields are reset after an update when using JSON manifests.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateResetsVersionSpecificFields_Json()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller);
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.FullJsonSingleton1_1", null, this.tempPath, null, manifestFormat: ManifestFormat.Json);
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            DefaultLocaleManifest updatedDefaultLocaleManifest = updatedManifests.DefaultLocaleManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            ClassicAssert.IsNull(updatedInstaller.ReleaseDateTime, "ReleaseDateTime at installer level should be null.");
            ClassicAssert.IsNull(updatedInstaller.ReleaseDate, "ReleaseDate at installer level should be null.");
            ClassicAssert.IsNull(updatedInstallerManifest.ReleaseDateTime, "ReleaseDateTime at root level should be null.");
            ClassicAssert.IsNull(updatedInstallerManifest.ReleaseDate, "ReleaseDate at root level should be null.");
            ClassicAssert.IsNull(updatedDefaultLocaleManifest.ReleaseNotes, "ReleaseNotes should be null.");
            ClassicAssert.IsNull(updatedDefaultLocaleManifest.ReleaseNotesUrl, "ReleaseNotesUrl should be null.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Portable, "InstallerType should be portable");
            ClassicAssert.IsTrue(updatedInstallerManifest.Commands[0] == "portableCommand", "Command value should be preserved.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType should be EXE");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var initialNestedInstallerFile = initialInstaller.NestedInstallerFiles.First();

            var updatedNestedInstallerFile = updatedInstallerManifest.NestedInstallerFiles.First();
            ClassicAssert.IsTrue(initialNestedInstallerFile.RelativeFilePath == updatedNestedInstallerFile.RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Portable, "NestedInstallerType should be PORTABLE");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var initialNestedInstallerFile = initialInstaller.NestedInstallerFiles.First();

            var updatedNestedInstallerFile = updatedInstallerManifest.NestedInstallerFiles.First();
            ClassicAssert.IsTrue(initialNestedInstallerFile.RelativeFilePath == updatedNestedInstallerFile.RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialNestedInstallerFile.PortableCommandAlias == updatedNestedInstallerFile.PortableCommandAlias, "PortableCommandAlias should be preserved.");
            ClassicAssert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedInstaller = updatedInstallerManifest.Installers.First();

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Msi, "NestedInstallerType should be MSI");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();

            ClassicAssert.IsTrue(initialInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedInstallerManifest.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialInstaller.InstallerSha256 != updatedInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsNotNull(updatedInstallerManifest.ProductCode, "ProductCode should be updated.");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedFirstInstaller = updatedInstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedInstallerManifest.Installers[1];

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Msix, "NestedInstallerType should be MSIX");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstallers = initialManifests.SingletonManifest.Installers;
            var initialFirstInstaller = initialInstallers[0];
            var initialSecondInstaller = initialInstallers[1];

            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedInstallerManifest.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");

            ClassicAssert.IsNotNull(updatedFirstInstaller.SignatureSha256, "SignatureSha256 should be updated.");
            ClassicAssert.IsNotNull(updatedSecondInstaller.SignatureSha256, "SignatureSha256 should be updated.");

            ClassicAssert.IsTrue(initialFirstInstaller.InstallerSha256 != updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialSecondInstaller.InstallerSha256 != updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that updating a zip package with multiple zip installers works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateMultipleZipInstallers()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);

            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipMultipleInstallers", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86", $"{installerUrl}|arm64" });

            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstallers = initialManifests.SingletonManifest.Installers;
            var initialFirstInstaller = initialInstallers[0];
            var initialSecondInstaller = initialInstallers[1];
            var initialThirdInstaller = initialInstallers[2];

            var updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedFirstInstaller = updatedInstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedInstallerManifest.Installers[1];
            var updatedThirdInstaller = updatedInstallerManifest.Installers[2];

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType should be EXE");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerFiles.Count == 1, "NestedInstallerFiles list should contain only one member");

            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedInstallerManifest.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedInstallerManifest.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            ClassicAssert.IsTrue(initialFirstInstaller.InstallerSha256 != updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialSecondInstaller.InstallerSha256 != updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialThirdInstaller.InstallerSha256 != updatedThirdInstaller.InstallerSha256, "InstallerSha256 should be updated");
        }

        /// <summary>
        /// Verifies that updating a zip package with multiple nested installer packages works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task UpdateZipWithMultipleNestedInstallers()
        {
            // Create copies of test exe installer to be used as portable installers
            List<string> portableFilePaths = TestUtils.CreateResourceCopy(TestConstants.TestExeInstaller, 4, TestConstants.TestPortableInstaller);

            // Add the generated portable installers to the test zip installer
            TestUtils.AddFilesToZip(TestConstants.TestZipInstaller, portableFilePaths);

            // Delete cached zip installer from other test runs so that the modified zip installer is downloaded
            TestUtils.DeleteCachedFiles(new List<string> { TestConstants.TestZipInstaller });

            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.ZipMultipleNestedInstallers", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86", $"{installerUrl}|arm", $"{installerUrl}|arm64|user", $"{installerUrl}|arm64|machine" });

            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);

            // Perform test clean up before any assertions
            portableFilePaths.ForEach(File.Delete);
            TestUtils.RemoveFilesFromZip(TestConstants.TestZipInstaller, portableFilePaths.Select(Path.GetFileName).ToList());

            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstallers = initialManifests.SingletonManifest.Installers;
            var initialFirstInstaller = initialInstallers[0];
            var initialSecondInstaller = initialInstallers[1];
            var initialThirdInstaller = initialInstallers[2];
            var initialFourthInstaller = initialInstallers[3];
            var initialFifthInstaller = initialInstallers[4];

            var updatedInstallerManifest = updatedManifests.InstallerManifest;
            var updatedFirstInstaller = updatedInstallerManifest.Installers[0];
            var updatedSecondInstaller = updatedInstallerManifest.Installers[1];
            var updatedThirdInstaller = updatedInstallerManifest.Installers[2];
            var updatedFourthInstaller = updatedInstallerManifest.Installers[3];
            var updatedFifthInstaller = updatedInstallerManifest.Installers[4];

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType should at the root level should be ZIP");

            ClassicAssert.AreEqual(NestedInstallerType.Portable, updatedFirstInstaller.NestedInstallerType, "Nested installer type should be portable");
            ClassicAssert.IsTrue(updatedFirstInstaller.NestedInstallerFiles.Count == 4, "NestedInstallerFiles list should contain four members");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedFirstInstaller.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedFirstInstaller.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[1].RelativeFilePath == updatedFirstInstaller.NestedInstallerFiles[1].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[1].PortableCommandAlias == updatedFirstInstaller.NestedInstallerFiles[1].PortableCommandAlias, "PortableCommandAlias should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[2].RelativeFilePath == updatedFirstInstaller.NestedInstallerFiles[2].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[2].PortableCommandAlias == updatedFirstInstaller.NestedInstallerFiles[2].PortableCommandAlias, "PortableCommandAlias should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[3].RelativeFilePath == updatedFirstInstaller.NestedInstallerFiles[3].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFirstInstaller.NestedInstallerFiles[3].PortableCommandAlias == updatedFirstInstaller.NestedInstallerFiles[3].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            // 2nd installer
            ClassicAssert.AreEqual(NestedInstallerType.Portable, updatedSecondInstaller.NestedInstallerType, "Nested installer type should be portable");
            ClassicAssert.IsTrue(updatedSecondInstaller.NestedInstallerFiles.Count == 2, "NestedInstallerFiles list should contain two members");
            ClassicAssert.IsTrue(initialSecondInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedSecondInstaller.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialSecondInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedSecondInstaller.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");
            ClassicAssert.IsTrue(initialSecondInstaller.NestedInstallerFiles[1].RelativeFilePath == updatedSecondInstaller.NestedInstallerFiles[1].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialSecondInstaller.NestedInstallerFiles[1].PortableCommandAlias == updatedSecondInstaller.NestedInstallerFiles[1].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            // 3rd installer
            ClassicAssert.AreEqual(NestedInstallerType.Portable, updatedThirdInstaller.NestedInstallerType, "Nested installer type should be portable");
            ClassicAssert.IsTrue(updatedThirdInstaller.NestedInstallerFiles.Count == 1, "NestedInstallerFiles list should contain only one member");
            ClassicAssert.IsTrue(initialThirdInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedThirdInstaller.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialThirdInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedThirdInstaller.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            // 4th installer
            ClassicAssert.AreEqual(NestedInstallerType.Exe, updatedFourthInstaller.NestedInstallerType, "Nested installer type should be EXE");
            ClassicAssert.IsTrue(updatedFourthInstaller.NestedInstallerFiles.Count == 1, "NestedInstallerFiles list should contain only one member");
            ClassicAssert.IsTrue(initialFourthInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedFourthInstaller.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFourthInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedFourthInstaller.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            // 5th installer
            ClassicAssert.AreEqual(NestedInstallerType.Msi, updatedFifthInstaller.NestedInstallerType, "Nested installer type should be MSI");
            ClassicAssert.IsTrue(updatedFifthInstaller.NestedInstallerFiles.Count == 1, "NestedInstallerFiles list should contain only one member");
            ClassicAssert.IsTrue(initialFifthInstaller.NestedInstallerFiles[0].RelativeFilePath == updatedFifthInstaller.NestedInstallerFiles[0].RelativeFilePath, "RelativeFilePath should be preserved.");
            ClassicAssert.IsTrue(initialFifthInstaller.NestedInstallerFiles[0].PortableCommandAlias == updatedFifthInstaller.NestedInstallerFiles[0].PortableCommandAlias, "PortableCommandAlias should be preserved.");

            // Hashes should be updated
            ClassicAssert.IsTrue(initialFirstInstaller.InstallerSha256 != updatedFirstInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialSecondInstaller.InstallerSha256 != updatedSecondInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialThirdInstaller.InstallerSha256 != updatedThirdInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialFourthInstaller.InstallerSha256 != updatedFourthInstaller.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.IsTrue(initialFifthInstaller.InstallerSha256 != updatedFifthInstaller.InstallerSha256, "InstallerSha256 should be updated");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType at the root level should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType at the root level should be EXE");
            ClassicAssert.IsTrue(updatedInstallerManifest.Scope == Scope.Machine, "Scope at the root level should be machine");
            ClassicAssert.IsTrue(updatedInstallerManifest.MinimumOSVersion == "10.0.22000.0", "MinimumOSVersion at the root level should be 10.0.22000.0");
            ClassicAssert.IsTrue(updatedInstallerManifest.PackageFamilyName == "TestPackageFamilyName", "PackageFamilyName at the root level should be TestPackageFamilyName");
            ClassicAssert.IsTrue(updatedInstallerManifest.UpgradeBehavior == UpgradeBehavior.Install, "UpgradeBehavior at the root level should be install");
            ClassicAssert.IsTrue(updatedInstallerManifest.ElevationRequirement == ElevationRequirement.ElevationRequired, "ElevationRequirement at the root level should be elevationRequired");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerAbortsTerminal == true, "InstallerAbortsTerminal at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallLocationRequired == true, "InstallLocation at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.RequireExplicitUpgrade == true, "RequireExplicitUpgrade at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.DisplayInstallWarnings == true, "DisplayInstallWarnings at the root level should be true");
            ClassicAssert.IsNotNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Platform, "Platform at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Commands, "Commands at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Protocols, "Protocols at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should not be null");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNotNull(updatedInstallerManifest.Markets, "Markets at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should not be null");
            foreach (var installer in updatedInstallerManifest.Installers)
            {
                ClassicAssert.IsNull(installer.InstallerType, "InstallerType at the installer level should be null");
                ClassicAssert.IsNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should be null");
                ClassicAssert.IsNull(installer.Scope, "Scope at the installer level should be null");
                ClassicAssert.IsNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should be null");
                ClassicAssert.IsNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should be null");
                ClassicAssert.IsNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should be null");
                ClassicAssert.IsNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallLocationRequired, "InstallLocation at the installer level should be null");
                ClassicAssert.IsNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should be null");
                ClassicAssert.IsNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should be null");
                ClassicAssert.IsNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should be null");
                ClassicAssert.IsNull(installer.Dependencies, "Dependencies at the installer level should be null");
                ClassicAssert.IsNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should be null");
                ClassicAssert.IsNull(installer.Platform, "Platform at the installer level should be null");
                ClassicAssert.IsNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should be null");
                ClassicAssert.IsNull(installer.Commands, "Commands at the installer level should be null");
                ClassicAssert.IsNull(installer.Protocols, "Protocols at the installer level should be null");
                ClassicAssert.IsNull(installer.FileExtensions, "FileExtensions at the installer level should be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // ClassicAssert.IsNull(installer.Markets, "Markets at the installer level should be null");
                ClassicAssert.IsNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should be null");
                ClassicAssert.IsNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should be null");
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
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            ClassicAssert.IsNull(updatedInstallerManifest.InstallerType, "InstallerType at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.NestedInstallerType, "NestedInstallerType at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Scope, "Scope at the root level should not be null");
            ClassicAssert.IsNull(updatedInstallerManifest.MinimumOSVersion, "MinimumOSVersion at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.PackageFamilyName, "PackageFamilyName at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UpgradeBehavior, "UpgradeBehavior at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.ElevationRequirement, "ElevationRequirement at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerAbortsTerminal, "InstallerAbortsTerminal at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallLocationRequired, "InstallLocation at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.RequireExplicitUpgrade, "RequireExplicitUpgrade at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.DisplayInstallWarnings, "DisplayInstallWarnings at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Platform, "Platform at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Commands, "Commands at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Protocols, "Protocols at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should be null");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNull(updatedInstallerManifest.Markets, "Markets at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should be null");

            foreach (var installer in updatedInstallerManifest.Installers)
            {
                ClassicAssert.IsNotNull(installer.InstallerType, "InstallerType at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.Scope, "Scope at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.InstallLocationRequired, "InstallLocation at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.Dependencies, "Dependencies at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.Platform, "Platform at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.Commands, "Commands at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.Protocols, "Protocols at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.FileExtensions, "FileExtensions at the installer level should not be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // ClassicAssert.IsNotNull(installer.Markets, "Markets at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should not be null");
                ClassicAssert.IsNotNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should not be null");
            }
        }

        /// <summary>
        /// Verifies that null installer fields are overwritten by root fields in update scenario.
        /// Expected flow:
        ///   1) Null installer level fields are overwritten by root fields at the start of the update.
        ///   2) The update flow modifies the installer level fields if needed. (e.g. ProductCode in case of MSI upgrade)
        ///   3) At the end of the update, the common installer fields are moved to the root level.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateOverwritesNullInstallerFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.OverwriteNullInstallerFields", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;

            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType at the root level should be ZIP");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerType == NestedInstallerType.Exe, "NestedInstallerType at the root level should be EXE");
            ClassicAssert.IsTrue(updatedInstallerManifest.Scope == Scope.Machine, "Scope at the root level should be machine");
            ClassicAssert.IsTrue(updatedInstallerManifest.MinimumOSVersion == "10.0.22000.0", "MinimumOSVersion at the root level should be 10.0.22000.0");
            ClassicAssert.IsTrue(updatedInstallerManifest.PackageFamilyName == "TestPackageFamilyName1", "PackageFamilyName at the root level should be TestPackageFamilyName");
            ClassicAssert.IsTrue(updatedInstallerManifest.UpgradeBehavior == UpgradeBehavior.Install, "UpgradeBehavior at the root level should be install");
            ClassicAssert.IsTrue(updatedInstallerManifest.ElevationRequirement == ElevationRequirement.ElevationRequired, "ElevationRequirement at the root level should be elevationRequired");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerAbortsTerminal == true, "InstallerAbortsTerminal at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallLocationRequired == true, "InstallLocation at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.RequireExplicitUpgrade == true, "RequireExplicitUpgrade at the root level should be true");
            ClassicAssert.IsTrue(updatedInstallerManifest.DisplayInstallWarnings == true, "DisplayInstallWarnings at the root level should be true");
            ClassicAssert.IsNotNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.NestedInstallerFiles[0].PortableCommandAlias == "PortableCommandAlias1", "PortableCommandAlias at the root level should be PortableCommandAlias1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerSwitches.Silent == "/silent1", "Silent installer switch at the root level should be /silent1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.Dependencies.PackageDependencies[0].PackageIdentifier == "TestPackageDependency1", "PackageDependencies PackageIdentifier at the root level should be TestPackageDependency1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.AppsAndFeaturesEntries[0].ProductCode == "TestProductCode1", "AppsAndFeaturesEntries ProductCode at the root level should be TestProduct1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Platform, "Platform at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.Platform[0] == Platform.Windows_Desktop, "Platform at the root level should contain Windows.Desktop");
            ClassicAssert.IsNotNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.ExpectedReturnCodes[0].InstallerReturnCode == 1001, "ExpectedReturnCodes InstallerReturnCode at the root level should be 1001");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Commands, "Commands at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.Commands[0] == "fakeCommand1", "Commands at the root level should contain fakeCommand1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.Protocols, "Protocols at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.Protocols[0] == "fakeProtocol1", "Protocols at the root level should contain fakeProtocol1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.FileExtensions[0] == ".exe", "FileExtensions at the root level should contain .exe");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNotNull(updatedInstallerManifest.Markets, "Markets at the root level should not be null");
            ClassicAssert.IsNotNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.UnsupportedOSArchitectures[0] == UnsupportedOSArchitecture.Arm64, "UnsupportedOSArchitectures at the root level should contain arm64");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerSuccessCodes[0] == 1, "InstallerSuccessCodes at the root level should contain 1");
            ClassicAssert.IsNotNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.UnsupportedArguments[1] == UnsupportedArgument.Location, "UnsupportedArguments at the root level should contain location");
            ClassicAssert.IsNotNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the root level should not be null");
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallationMetadata.DefaultInstallLocation == "%ProgramFiles%\\TestApp1", "DefaultInstallLocation at the root level should be ProgramFiles/TestApp1");
            foreach (var installer in updatedInstallerManifest.Installers)
            {
                ClassicAssert.IsNull(installer.InstallerType, "InstallerType at the installer level should be null");
                ClassicAssert.IsNull(installer.NestedInstallerType, "NestedInstallerType at the installer level should be null");
                ClassicAssert.IsNull(installer.Scope, "Scope at the installer level should be null");
                ClassicAssert.IsNull(installer.MinimumOSVersion, "MinimumOSVersion at the installer level should be null");
                ClassicAssert.IsNull(installer.PackageFamilyName, "PackageFamilyName at the installer level should be null");
                ClassicAssert.IsNull(installer.UpgradeBehavior, "UpgradeBehavior at the installer level should be null");
                ClassicAssert.IsNull(installer.ElevationRequirement, "ElevationRequirement at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerAbortsTerminal, "InstallerAbortsTerminal at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallLocationRequired, "InstallLocation at the installer level should be null");
                ClassicAssert.IsNull(installer.RequireExplicitUpgrade, "RequireExplicitUpgrade at the installer level should be null");
                ClassicAssert.IsNull(installer.DisplayInstallWarnings, "DisplayInstallWarnings at the installer level should be null");
                ClassicAssert.IsNull(installer.NestedInstallerFiles, "NestedInstallerFiles at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerSwitches, "InstallerSwitches at the installer level should be null");
                ClassicAssert.IsNull(installer.Dependencies, "Dependencies at the installer level should be null");
                ClassicAssert.IsNull(installer.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the installer level should be null");
                ClassicAssert.IsNull(installer.Platform, "Platform at the installer level should be null");
                ClassicAssert.IsNull(installer.ExpectedReturnCodes, "ExpectedReturnCodes at the installer level should be null");
                ClassicAssert.IsNull(installer.Commands, "Commands at the installer level should be null");
                ClassicAssert.IsNull(installer.Protocols, "Protocols at the installer level should be null");
                ClassicAssert.IsNull(installer.FileExtensions, "FileExtensions at the installer level should be null");

                // TODO: Uncomment when installer model gets updated to support markets field.
                // ClassicAssert.IsNull(installer.Markets, "Markets at the installer level should be null");
                ClassicAssert.IsNull(installer.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should be null");
                ClassicAssert.IsNull(installer.UnsupportedArguments, "UnsupportedArguments at the installer level should be null");
                ClassicAssert.IsNull(installer.InstallationMetadata, "InstallationMetadata at the installer level should be null");
            }
        }

        /// <summary>
        /// Verifies that non-null installer fields are preserved when overwriting with root fields at the start of the update.
        /// Expected flow:
        ///   1) For the first installer, all root level fields are copied over and root fields are set to null.
        ///   2) For the second installer, installer level fields are preserved since they are not null.
        ///   3) InstallerType, NestedInstallerType and NestedInstallerFiles are common across both installers, so they are moved to the root level at the end of the update.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateRetainsNonNullInstallerFields()
        {
            TestUtils.InitializeMockDownloads(TestConstants.TestZipInstaller);
            string installerUrl = $"https://fakedomain.com/{TestConstants.TestZipInstaller}";
            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.RetainInstallerFields", null, this.tempPath, new[] { $"{installerUrl}|x64", $"{installerUrl}|x86" });
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            ClassicAssert.IsNotNull(updatedManifests, "Command should have succeeded");

            InstallerManifest updatedInstallerManifest = updatedManifests.InstallerManifest;
            Installer firstInstaller = updatedInstallerManifest.Installers[0];
            Installer secondInstaller = updatedInstallerManifest.Installers[1];

            // Fields for first installer should be copied over from root
            ClassicAssert.IsTrue(firstInstaller.Scope == Scope.Machine, "Scope for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.MinimumOSVersion == "10.0.22000.0", "MinimumOSVersion for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.PackageFamilyName == "TestPackageFamilyName1", "PackageFamilyName for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.UpgradeBehavior == UpgradeBehavior.Install, "UpgradeBehavior for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.ElevationRequirement == ElevationRequirement.ElevationRequired, "ElevationRequirement for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.InstallerAbortsTerminal == true, "InstallerAbortsTerminal for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.InstallLocationRequired == true, "InstallLocation for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.RequireExplicitUpgrade == true, "RequireExplicitUpgrade for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.DisplayInstallWarnings == true, "DisplayInstallWarnings for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.NestedInstallerFiles, "NestedInstallerFiles for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.NestedInstallerFiles[0].RelativeFilePath == "WingetCreateTestExeInstaller.exe", "RelativeFilePath for the first installer should be copied over from root");
            ClassicAssert.IsTrue(firstInstaller.NestedInstallerFiles[0].PortableCommandAlias == "TestExeAlias", "PortableCommandAlias for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.InstallerSwitches, "InstallerSwitches for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.InstallerSwitches.Silent == "/silent1", "Silent installer switch for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.Dependencies, "Dependencies for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.Dependencies.PackageDependencies[0].PackageIdentifier == "TestPackageDependency1", "PackageDependencies PackageIdentifier for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.AppsAndFeaturesEntries, "AppsAndFeaturesEntries for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.AppsAndFeaturesEntries[0].ProductCode == "TestProductCode1", "AppsAndFeaturesEntries ProductCode for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.Platform, "Platform for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.Platform[0] == Platform.Windows_Desktop, "Platform for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.ExpectedReturnCodes, "ExpectedReturnCodes for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.ExpectedReturnCodes[0].InstallerReturnCode == 1001, "ExpectedReturnCodes InstallerReturnCode for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.Commands, "Commands for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.Commands[0] == "fakeCommand1", "Commands for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.Protocols, "Protocols for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.Protocols[0] == "fakeProtocol1", "Protocols for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.FileExtensions, "FileExtensions for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.FileExtensions[0] == ".exe", "FileExtensions for the first installer should be copied over from root");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNotNull(firstInstaller.Markets, "Markets for the first installer should not be null");
            ClassicAssert.IsNotNull(firstInstaller.UnsupportedOSArchitectures, "UnsupportedOSArchitectures for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.UnsupportedOSArchitectures[0] == UnsupportedOSArchitecture.Arm64, "UnsupportedOSArchitectures for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.InstallerSuccessCodes, "InstallerSuccessCodes for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.InstallerSuccessCodes[0] == 1, "InstallerSuccessCodes for the first installer should contain be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.UnsupportedArguments, "UnsupportedArguments for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.UnsupportedArguments[1] == UnsupportedArgument.Location, "UnsupportedArguments for the first installer should be copied over from root");
            ClassicAssert.IsNotNull(firstInstaller.InstallationMetadata, "InstallationMetadata for the first installer should not be null");
            ClassicAssert.IsTrue(firstInstaller.InstallationMetadata.DefaultInstallLocation == "%ProgramFiles%\\TestApp1", "DefaultInstallLocation for the first installer should be copied over from root");

            // Fields for second installer should be preserved
            ClassicAssert.IsTrue(secondInstaller.Scope == Scope.User, "Scope for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.MinimumOSVersion == "10.0.17763.0", "MinimumOSVersion for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.PackageFamilyName == "TestPackageFamilyName2", "PackageFamilyName for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.UpgradeBehavior == UpgradeBehavior.UninstallPrevious, "UpgradeBehavior for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.ElevationRequirement == ElevationRequirement.ElevatesSelf, "ElevationRequirement for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.InstallerAbortsTerminal == false, "InstallerAbortsTerminal for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.InstallLocationRequired == false, "InstallLocation for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.RequireExplicitUpgrade == false, "RequireExplicitUpgrade for the second installer should be preserved");
            ClassicAssert.IsTrue(secondInstaller.DisplayInstallWarnings == false, "DisplayInstallWarnings for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.NestedInstallerFiles, "NestedInstallerFiles for the first installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.NestedInstallerFiles[0].RelativeFilePath == "WingetCreateTestMsiInstaller.msi", "RelativeFilePath for the second installer should be copied over from root");
            ClassicAssert.IsTrue(secondInstaller.NestedInstallerFiles[0].PortableCommandAlias == "TestMsiAlias", "PortableCommandAlias for the second installer should be copied over from root");
            ClassicAssert.IsNotNull(secondInstaller.InstallerSwitches, "InstallerSwitches for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.InstallerSwitches.Silent == "/silent2", "Silent installer switch for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.Dependencies, "Dependencies for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.Dependencies.PackageDependencies[0].PackageIdentifier == "TestPackageDependency2", "PackageDependencies PackageIdentifier for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.AppsAndFeaturesEntries, "AppsAndFeaturesEntries for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.AppsAndFeaturesEntries[0].ProductCode == "TestProductCode2", "AppsAndFeaturesEntries ProductCode for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.Platform, "Platform for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.Platform[0] == Platform.Windows_Universal, "Platform for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.ExpectedReturnCodes, "ExpectedReturnCodes for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.ExpectedReturnCodes[0].InstallerReturnCode == 1002, "ExpectedReturnCodes InstallerReturnCode for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.Commands, "Commands for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.Commands[0] == "fakeCommand2", "Commands for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.Protocols, "Protocols for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.Protocols[0] == "fakeProtocol2", "Protocols for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.FileExtensions, "FileExtensions for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.FileExtensions[0] == ".msi", "FileExtensions for the second installer should be preserved");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNotNull(secondInstaller.Markets, "Markets for the first installer should not be null");
            ClassicAssert.IsNotNull(secondInstaller.UnsupportedOSArchitectures, "UnsupportedOSArchitectures for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.UnsupportedOSArchitectures[0] == UnsupportedOSArchitecture.Arm, "UnsupportedOSArchitectures for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.InstallerSuccessCodes, "InstallerSuccessCodes for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.InstallerSuccessCodes[0] == 2, "InstallerSuccessCodes for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.UnsupportedArguments, "UnsupportedArguments for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.UnsupportedArguments[0] == UnsupportedArgument.Log, "UnsupportedArguments for the second installer should be preserved");
            ClassicAssert.IsNotNull(secondInstaller.InstallationMetadata, "InstallationMetadata for the second installer should not be null");
            ClassicAssert.IsTrue(secondInstaller.InstallationMetadata.DefaultInstallLocation == "%ProgramFiles%\\TestApp2", "DefaultInstallLocation for the second installer should be preserved");

            // Root fields should be null
            ClassicAssert.IsNull(updatedInstallerManifest.Scope, "Scope at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.NestedInstallerFiles, "NestedInstallerFiles at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.NestedInstallerType, "NestedInstallerType at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.MinimumOSVersion, "MinimumOSVersion at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.PackageFamilyName, "PackageFamilyName at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UpgradeBehavior, "UpgradeBehavior at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.ElevationRequirement, "ElevationRequirement at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerAbortsTerminal, "InstallerAbortsTerminal at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallLocationRequired, "InstallLocation at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.RequireExplicitUpgrade, "RequireExplicitUpgrade at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.DisplayInstallWarnings, "DisplayInstallWarnings at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerSwitches, "InstallerSwitches at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Dependencies, "Dependencies at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.AppsAndFeaturesEntries, "AppsAndFeaturesEntries at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Platform, "Platform at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.ExpectedReturnCodes, "ExpectedReturnCodes at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Commands, "Commands at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.Protocols, "Protocols at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.FileExtensions, "FileExtensions at the root level should be null");

            // TODO: Uncomment when installer model gets updated to support markets field.
            // ClassicAssert.IsNull(updatedInstallerManifest.Markets, "Markets at the root level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UnsupportedOSArchitectures, "UnsupportedOSArchitectures at the installer level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallerSuccessCodes, "InstallerSuccessCodes at the installer level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.UnsupportedArguments, "UnsupportedArguments at the installer level should be null");
            ClassicAssert.IsNull(updatedInstallerManifest.InstallationMetadata, "InstallationMetadata at the installer level should be null");

            // Fields that should be moved to root
            ClassicAssert.IsTrue(updatedInstallerManifest.InstallerType == InstallerType.Zip, "InstallerType at the root level should be ZIP");
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
            ClassicAssert.IsNull(updatedManifests, "Command should have failed");
            string result = this.sw.ToString();
            Assert.That(result, Does.Contain(string.Format(Resources.NestedInstallerFileNotFound_Error, "fakeRelativeFilePath.exe")), "Failed to show warning for invalid relative file path.");
        }

        private static (UpdateCommand UpdateCommand, List<string> InitialManifestContent) GetUpdateCommandAndManifestData(string id, string version, string outputDir, IEnumerable<string> installerUrls, bool isMultifile = false, ManifestFormat manifestFormat = ManifestFormat.Yaml)
        {
            string fileExtension = (manifestFormat == ManifestFormat.Yaml) ? ".yaml" : ".json";
            var updateCommand = new UpdateCommand
            {
                Id = id,
                Version = version,
                OutputDir = outputDir,
                Format = manifestFormat,
            };

            if (installerUrls != null)
            {
                updateCommand.InstallerUrls = installerUrls;
            }

            var initialManifestContent = isMultifile ? TestUtils.GetInitialMultifileManifestContent(id) : TestUtils.GetInitialManifestContent($"{id}{fileExtension}");
            return (updateCommand, initialManifestContent);
        }

        private static async Task<Manifests> RunUpdateCommand(UpdateCommand updateCommand, List<string> initialManifestContent)
        {
            Manifests initialManifests = updateCommand.DeserializeManifestContentAndApplyInitialUpdate(initialManifestContent);
            return await updateCommand.UpdateManifestsAutonomously(initialManifests);
        }
    }
}
