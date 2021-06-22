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
    using Microsoft.WingetCreateCore.Models.Settings;
    using Newtonsoft.Json;

    /// <summary>
    /// Command to either update or delete the cached GitHub Oauth token.
    /// </summary>
    [Verb("settings", HelpText = "TokenCommand_HelpText", ResourceType = typeof(Resources))]
    public class SettingsCommand : BaseCommand
    {
        /// <summary>
        /// Gets the static instance of the settings model used for Winget-Create.
        /// </summary>
        public static SettingsManifest SettingsManifest
        {
            get
            {
                InitializeSettingsFiles();
                using (StreamReader r = new StreamReader(Common.SettingsJsonPath))
                {
                    string json = r.ReadToEnd();
                    return JsonConvert.DeserializeObject<SettingsManifest>(json);
                }
            }
        }

        /// <summary>
        /// Recreates a settings json file using the default values from the settings schema model;
        /// </summary>
        public static void GenerateDefaultSettingsFile()
        {
            SettingsManifest defaultSettings = new SettingsManifest { Telemetry = new Telemetry() };
            string output = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            File.WriteAllText(Common.SettingsJsonPath, output);
            File.WriteAllText(Common.SettingsBackupJsonPath, output);
        }

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
                InitializeSettingsFiles();
                this.OpenJsonFile(Common.SettingsJsonPath);
                return await Task.FromResult(commandEvent.IsSuccessful = true);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private static void InitializeSettingsFiles()
        {
            if (File.Exists(Common.SettingsJsonPath) && IsValidSettingsJson(Common.SettingsJsonPath))
            {
                File.Copy(Common.SettingsJsonPath, Common.SettingsBackupJsonPath, true);
            }
            else if (File.Exists(Common.SettingsBackupJsonPath) && IsValidSettingsJson(Common.SettingsBackupJsonPath))
            {
                File.Copy(Common.SettingsBackupJsonPath, Common.SettingsJsonPath, true);
            }
            else
            {
                GenerateDefaultSettingsFile();
            }
        }

        private static bool IsValidSettingsJson(string path)
        {
            SettingsManifest settingsManifest;

            try
            {
                using (StreamReader r = new StreamReader(path))
                {
                    string json = r.ReadToEnd();
                    settingsManifest = JsonConvert.DeserializeObject<SettingsManifest>(json);
                }
            }
            catch (JsonException)
            {
                Logger.Error($"Unable to parse {path}");
                return false;
            }

            return settingsManifest != null;
        }

        private void OpenJsonFile(string path)
        {
            Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
        }
    }
}
