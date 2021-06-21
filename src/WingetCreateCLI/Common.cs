// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using Microsoft.Diagnostics.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Sharprompt;
    using System;
    using System.Diagnostics.Tracing;
    using System.IO;
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

        /// <summary>
        /// Gets directory path where app should store local state.
        /// </summary>
        public static string LocalAppStatePath => AppStatePathLazy.Value;

        /// <summary>
        /// Checks if the tool is being launched for the first time.
        /// </summary>
        public static void FirstRunTelemetryConsent()
        {
            string firstRunFilePath = Path.Combine(LocalAppStatePath, FirstRunFileName);

            if (!File.Exists(firstRunFilePath))
            {
                Prompt.Confirm("We've detected that this is your first run. Would you like to enable telemetry to collect data for " +
                    "Microsoft to make improvements to this tool?");
                File.Create(firstRunFilePath);
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
