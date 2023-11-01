// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;

    /// <summary>
    /// Info command to display general information regarding the tool.
    /// </summary>
    [Verb("info", HelpText = "InfoCommand_HelpText", ResourceType = typeof(Resources))]
    public class InfoCommand : BaseCommand
    {
        /// <summary>
        /// Executes the info command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent();

            DisplayApplicationHeaderAndCopyright();
            Console.WriteLine();
            DisplaySystemInformation();
            Console.WriteLine();
            DisplayInfoTable();

            TelemetryManager.Log.WriteEvent(commandEvent);
            return await Task.FromResult(commandEvent.IsSuccessful = true);
        }

        private static void DisplayApplicationHeaderAndCopyright()
        {
            Console.WriteLine(string.Format(
                Resources.Heading,
                Utils.GetEntryAssemblyVersion()) +
                Environment.NewLine +
                Constants.MicrosoftCopyright);
        }

        private static void DisplaySystemInformation()
        {
            Logger.DebugLocalized(nameof(Resources.OperatingSystem_Info), Environment.OSVersion.VersionString);
            Logger.DebugLocalized(nameof(Resources.SystemArchitecture_Info), System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
        }

        private static void DisplayInfoTable()
        {
            string logsdirectory = Common.GetPathForDisplay(Path.Combine(Common.LocalAppStatePath, "DiagOutputDir"), UserSettings.AnonymizePaths);
            string settingsDirectory = Common.GetPathForDisplay(UserSettings.SettingsJsonPath, UserSettings.AnonymizePaths);
            string installerCacheDirectory = Common.GetPathForDisplay(PackageParser.InstallerDownloadPath, UserSettings.AnonymizePaths);

            new TableOutput(Resources.WingetCreateDirectories_Heading, Resources.Path_Heading)
                            .AddRow(Resources.Logs_Heading, logsdirectory)
                            .AddRow(Resources.UserSettings_Heading, settingsDirectory)
                            .AddRow(Resources.InstallerCache_Heading, installerCacheDirectory)
                            .Print();

            Console.WriteLine();

            new TableOutput(Resources.Links_Heading, string.Empty)
                            .AddRow(Resources.PrivacyStatement_Heading, "https://aka.ms/winget-create-privacy")
                            .AddRow(Resources.LicenseAgreement_Heading, "https://aka.ms/winget-create-license")
                            .AddRow(Resources.ThirdPartyNotices_Heading, "https://aka.ms/winget-create-3rdPartyNotices")
                            .AddRow(Resources.Homepage_Heading, "https://aka.ms/winget-create")
                            .Print();
        }
    }
}
