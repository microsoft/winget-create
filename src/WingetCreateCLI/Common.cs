// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Diagnostics.Tracing;
    using System.IO;
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
                File.Create(firstRunFilePath);
                Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
                Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);
                Console.WriteLine("Welcome to Winget-Create!");
                Console.WriteLine();
                Console.WriteLine("Telemetry Settings");
                Console.WriteLine("------------------");
                Console.WriteLine("The Windows Package Manager Manifest Creator collects usage data in order to improve your experience.");
                Console.WriteLine("The data is collected by Microsoft and is anonymous.");
                Prompt.Confirm("Would you like to enable telemetry?");
                UserSettings.TelemetryDisabled = !Prompt.Confirm(Properties.Resources.EnableTelemetryFirstRun_Message);

                // Write docs for command
                // Submit PR for review
                // Address bugs assigned to me by Arjun (2 bugs, temp files and character printing)

            }
        }

        private static bool IsRunningAsUwp()
        {
            DesktopBridge.Helpers helpers = new DesktopBridge.Helpers();
            return helpers.IsRunningAsUwp();
        }
    }
}
