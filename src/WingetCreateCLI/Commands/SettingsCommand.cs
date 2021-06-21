// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models.Settings;
    using Newtonsoft.Json;

    /// <summary>
    /// Command to either update or delete the cached GitHub Oauth token.
    /// </summary>
    [Verb("settings", HelpText = "TokenCommand_HelpText", ResourceType = typeof(Resources))]
    public class SettingsCommand : BaseCommand
    {
        private string SettingsJsonPath => Path.Combine(Common.LocalAppStatePath, "settings.json");

        private string BackupSettingsJsonPath => Path.Combine(Common.LocalAppStatePath, "settings.backup.json");

        /// <summary>
        /// Executes the token command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(SettingsCommand),
            };

            try
            {
                string settingsPath = Path.Combine(Common.LocalAppStatePath, "settings.json");

                if (!File.Exists(settingsPath))
                {
                    File.Create(settingsPath);
                }
                //else
                //{
                //    Process.Start(settingsPath);
                //}
                this.GenerateSettingsFile();

                return await Task.FromResult(commandEvent.IsSuccessful = true);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        // Checks to verify that both the settings and backup settings file are created.
        private void GenerateSettingsFile()
        {
            SettingsManifest settingsModel = new SettingsManifest { Telemetry = new Telemetry() };
            settingsModel.Telemetry = new Telemetry();
            string output = JsonConvert.SerializeObject(settingsModel);
            File.WriteAllText(this.BackupSettingsJsonPath, output);

            if (!File.Exists(this.SettingsJsonPath))
            {
                File.WriteAllText(this.SettingsJsonPath, output);
            }
        }
    }
}
