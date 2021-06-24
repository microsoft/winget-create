// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
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
            if (File.Exists(UserSettings.SettingsJsonPath))
            {
                File.Delete(UserSettings.SettingsJsonPath);
            }

            if (File.Exists(UserSettings.SettingsBackupJsonPath))
            {
                File.Delete(UserSettings.SettingsBackupJsonPath);
            }
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
            Assert.IsTrue(UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath).IsValid, "Generated settings file was not valid.");
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
            Assert.IsTrue(UserSettings.ParseJsonFile(UserSettings.SettingsBackupJsonPath).IsValid, "Generated backup settings file was not valid.");
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
            Assert.IsFalse(UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath).IsValid, "Empty settings manifest should fail validation");
            StringAssert.Contains(
                "Manifest cannot be empty",
                UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath).Errors.First(),
                "Error message should be caught.");
        }

        /// <summary>
        /// Compares the state of the telemetry.disabled field before and after to ensure the change is reflected accordingly.
        /// </summary>
        [Test]
        public void VerifySavingTelemetrySettings()
        {
            bool isDisable = UserSettings.TelemetryDisable;
            UserSettings.TelemetryDisable = !isDisable;
            SettingsManifest settings = UserSettings.LoadJsonFile(UserSettings.SettingsJsonPath);
            Assert.IsTrue(settings.Telemetry.Disable == !isDisable, "Changed telemetry setting was not reflected in the settings file.");
        }
    }
}
