// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore;

    /// <summary>
    /// Command line options for the root command.
    /// </summary>
    internal class RootOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not to display the general info text.
        /// </summary>
        [Option("info", Required = false, HelpText = "InfoRootOption_HelpText", ResourceType = typeof(Resources))]
        public bool Info { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to display the help text.
        /// </summary>
        [Option("help", Required = false, HelpText = "HelpRootOption_HelpText", ResourceType = typeof(Resources))]
        public bool Help { get; set; }

        /// <summary>
        /// Parses the root options and displays the appropriate output.
        /// </summary>
        /// <param name="rootOptions">Root options to execute.</param>
        public static void ParseRootOptions(RootOptions rootOptions)
        {
            if (rootOptions.Info)
            {
                Program.DisplayApplicationHeaderAndCopyright();
                Console.WriteLine();
                DisplaySystemInformation();
                Console.WriteLine();
                DisplayInfoTable();
            }
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
