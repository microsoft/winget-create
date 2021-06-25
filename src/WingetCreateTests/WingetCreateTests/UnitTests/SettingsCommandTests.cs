// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using NUnit.Framework;

    /// <summary>
    /// Unit test class for the Settings Command.
    /// </summary>
    public class SettingsCommandTests
    {
        /// <summary>
        /// OneTimeSetup method for the settings command unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Logger.Initialize();
        }

        /// <summary>
        /// Setup method that removes the settings file and backup settings file prior to running tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            File.Delete(UserSettings.SettingsJsonPath);
            File.Delete(UserSettings.SettingsBackupJsonPath);
        }

        /// <summary>
        /// Runs the settings command once which should generate a new settings file.
        /// </summary>
        [Test]
        public void VerifySettingsFileGenerated()
        {
            SettingsCommand command = new SettingsCommand();
            command.Execute();
            Assert.IsTrue(File.Exists(UserSettings.SettingsJsonPath), "Settings file was not created successfully.");
            Assert.IsTrue(UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath, out SettingsManifest manifest).IsValid, "Generated settings file was not valid.");
        }

        /// <summary>
        /// Runs the settings command twice which should generate a second backup settings file.
        /// </summary>
        [Test]
        public void VerifyBackupSettingsFileGenerated()
        {
            SettingsCommand firstCommand = new SettingsCommand();
            firstCommand.Execute();
            SettingsCommand secondCommand = new SettingsCommand();
            secondCommand.Execute();
            Assert.IsTrue(File.Exists(UserSettings.SettingsBackupJsonPath), "Backup settings file was not created successfully.");
            Assert.IsTrue(UserSettings.ParseJsonFile(UserSettings.SettingsBackupJsonPath, out SettingsManifest manifest).IsValid, "Generated backup settings file was not valid.");
        }

        /// <summary>
        /// Runs the settings command and modifies the file which should display a "Manifest cannot be empty." error.
        /// </summary>
        [Test]
        public void VerifyEmptySettingsFile()
        {
            SettingsCommand command = new SettingsCommand();
            command.Execute();
            File.WriteAllText(UserSettings.SettingsJsonPath, string.Empty);
            (bool isValid, List<string> errors) = UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath, out SettingsManifest manifest);
            Assert.IsFalse(isValid, "Empty settings manifest should fail validation");
            StringAssert.Contains("Manifest cannot be empty", errors.First(), "Error message should be caught.");
        }

        /// <summary>
        /// Compares the state of the telemetry.disabled field before and after to ensure the change is reflected accordingly.
        /// </summary>
        [Test]
        public void VerifySavingTelemetrySettings()
        {
            bool isDisabled = UserSettings.TelemetryDisabled;
            UserSettings.TelemetryDisabled = !isDisabled;
            UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath, out SettingsManifest manifest);
            Assert.IsTrue(manifest.Telemetry.Disable == !isDisabled, "Changed telemetry setting was not reflected in the settings file.");
        }
    }
}
