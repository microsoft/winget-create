﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
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
                    Logger.WarnLocalized(nameof(Resources.GenerateNewSettingsFile_Message));
                    UserSettings.SaveSettings();
                }
                else
                {
                    (bool isSettingsValid, List<string> settingsFileErrors) = UserSettings.ParseJsonFile(UserSettings.SettingsJsonPath, out SettingsManifest manifest);

                    if (isSettingsValid)
                    {
                        File.Copy(UserSettings.SettingsJsonPath, UserSettings.SettingsBackupJsonPath, true);
                    }
                    else
                    {
                        DisplayParsingErrors(settingsFileErrors, UserSettings.SettingsJsonPath);
                    }
                }

                if (File.Exists(UserSettings.SettingsBackupJsonPath))
                {
                    (bool isBackupValid, List<string> backupFileErrors) = UserSettings.ParseJsonFile(UserSettings.SettingsBackupJsonPath, out SettingsManifest manifest);

                    if (!isBackupValid)
                    {
                        DisplayParsingErrors(backupFileErrors, UserSettings.SettingsBackupJsonPath);
                    }
                }

                return Task.FromResult(commandEvent.IsSuccessful = OpenJsonFile(UserSettings.SettingsJsonPath));
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private static void DisplayParsingErrors(List<string> errors, string path)
        {
            Logger.WarnLocalized(nameof(Resources.ErrorParsingSettingsFile_Message), Path.GetFileName(path));

            Console.WriteLine();
            foreach (string e in errors)
            {
                Logger.Warn(e);
            }
        }

        private static bool OpenJsonFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = path });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", path);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

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
