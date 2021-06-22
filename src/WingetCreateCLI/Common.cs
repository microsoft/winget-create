// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Diagnostics.Tracing;
    using System.IO;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Sharprompt;
    using Windows.Storage;

    /// <summary>
    /// Helper class containing common functionality for the CLI.
    /// </summary>
    public static class Common
    {
        private const string ModuleName = "WindowsPackageManagerManifestCreator";

        private const string FirstRunFileName = "FirstRun.txt";

        private static readonly Lazy<string> AppStatePathLazy = new(() =>
            IsRunningAsUwp()
            ? ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", ModuleName));

        private static readonly Lazy<string> SettingsPathLazy = new(() => Path.Combine(LocalAppStatePath, "settings.json"));

        private static readonly Lazy<string> SettingsBackupPathLazy = new(() => Path.Combine(LocalAppStatePath, "settings.backup.json"));

        /// <summary>
        /// Gets directory path where app should store local state.
        /// </summary>
        public static string LocalAppStatePath => AppStatePathLazy.Value;

        /// <summary>
        /// Gets the path to the settings json file.
        /// </summary>
        public static string SettingsJsonPath => SettingsPathLazy.Value;

        /// <summary>
        /// Gets the path to the backup settings json file.
        /// </summary>
        public static string SettingsBackupJsonPath => SettingsBackupPathLazy.Value;

        /// <summary>
        /// Checks if the tool is being launched for the first time.
        /// </summary>
        public static void FirstRunTelemetryConsent()
        {
            string firstRunFilePath = Path.Combine(LocalAppStatePath, FirstRunFileName);

            if (!File.Exists(firstRunFilePath))
            {
                File.Create(firstRunFilePath);
                SettingsCommand.GenerateDefaultSettingsFile();
                Prompt.Confirm("We've detected that this is your first run. Would you like to enable telemetry to collect data for " +
                    "Microsoft to make improvements to this tool?");

                // Handle confirmation for telemetry.
                var eventListener = new TelemetryEventListener();
                eventListener.DisableEvents(new EventSource("Microsoft.PackageManager.Create"));
            }
        }

        private static bool IsRunningAsUwp()
        {
            DesktopBridge.Helpers helpers = new DesktopBridge.Helpers();
            return helpers.IsRunningAsUwp();
        }
    }
}
