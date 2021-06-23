// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;

    /// <summary>
    /// Command to either update or delete the cached GitHub Oauth token.
    /// </summary>
    [Verb("settings", HelpText = "SettingsCommand_HelpText", ResourceType = typeof(Resources))]
    public class SettingsCommand : BaseCommand
    {
        /// <summary>
        /// Executes the token command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(SettingsCommand),
            };

            try
            {
                if (!File.Exists(UserSettings.SettingsJsonPath))
                {
                    UserSettings.GenerateFileFromLoadedSettings(UserSettings.SettingsJsonPath);
                }

                return Task.FromResult(commandEvent.IsSuccessful = this.OpenJsonFile(UserSettings.SettingsJsonPath));
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private bool OpenJsonFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
                return true;
            }
            catch (Win32Exception e)
            {
                Logger.ErrorLocalized(nameof(Resources.Error_Prefix), e.Message);
                return false;
            }
        }
    }
}
