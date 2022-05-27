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
        public async Task UpdateBasedOnInstallerUrlMatch()
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

            (UpdateCommand command, var initialManifestContent) = GetUpdateCommandAndManifestData("TestPublisher.MatchWithInstallerUrl", null, this.tempPath, null);
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
            Assert.That(result, Does.Contain(string.Format(Resources.UnableToParseArchOverride_Error, invalidArch)), "Failed to show architecture override parsing error.");
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
        /// Test the architecture override with multiple installers. Verifies that if the override architecture does not match any installer,
        /// the architecture detected from the url or the binary will be used instead to perform the matching.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        [Test]
        public async Task UpdateWithMultipleArchitectureOverrides()
        {
            var expectedArchs = new[]
            {
                Architecture.Arm,
                Architecture.X64,
            };

            string x64ExeInstallerUrl = $"https://fakedomain.com/{TestConstants.TestExeInstaller}";
            string x86ExeInstallerUrl = $"https://fakedomain.com/win32/{TestConstants.TestExeInstaller}";
            TestUtils.InitializeMockDownloads(TestConstants.TestExeInstaller, $"win32/{TestConstants.TestExeInstaller}");
            (UpdateCommand command, var initialManifestContent) =
                GetUpdateCommandAndManifestData("TestPublisher.MultipleArchitectureOverride", "1.2.3.4", this.tempPath, new[] { $"{x64ExeInstallerUrl}|arm", $"{x86ExeInstallerUrl}|x64" });

            var initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            var initialInstaller = initialManifests.SingletonManifest.Installers.First();
            var updatedManifests = await RunUpdateCommand(command, initialManifestContent);
            Assert.IsNotNull(updatedManifests, "Command should have succeeded");

            foreach (var item in expectedArchs.Zip(updatedManifests.InstallerManifest.Installers, (expectedArch, installer) => (expectedArch, installer)))
            {
                Assert.AreEqual(item.expectedArch, item.installer.Architecture, "Architecture override failed.");
                Assert.AreNotEqual(initialInstaller.InstallerSha256, item.installer.InstallerSha256, "InstallerSha256 should be updated");
            }
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
            var updatedInstaller = updatedManifests.InstallerManifest.Installers.First();
            Assert.AreEqual("FakeProductCode", updatedInstaller.ProductCode, "Existing value for ProductCode was overwritten.");
            Assert.AreEqual("FakePackageFamilyName", updatedInstaller.PackageFamilyName, "Existing value for PackageFamilyName was overwritten.");
            Assert.IsNotNull(updatedInstaller.Platform, "Existing value for Platform was overwritten.;");
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
            foreach (var updatedInstaller in updatedManifests.InstallerManifest.Installers)
            {
                Assert.AreEqual(InstallerType.Appx, updatedInstaller.InstallerType, "Msix installerType should be matched with Appx");
            }
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
        /// Ensures that all fields from the Singleton v1.1. manifest can be deserialized and updated correctly.
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

            var initialManifestContent = TestUtils.GetInitialManifestContent($"{id}.yaml");

            return (updateCommand, initialManifestContent);
        }

        private static async Task<Manifests> RunUpdateCommand(UpdateCommand updateCommand, List<string> initialManifestContent)
        {
            Manifests initialManifests = updateCommand.DeserializeManifestContentAndApplyInitialUpdate(initialManifestContent);
            return await updateCommand.UpdateManifestsAutonomously(initialManifests);
        }
    }
}
