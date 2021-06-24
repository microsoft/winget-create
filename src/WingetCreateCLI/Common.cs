// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using Microsoft.WingetCreateCLI.Properties;
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
                Console.WriteLine(Resources.TelemetrySettings_Message);
                Console.WriteLine("------------------");
                Console.WriteLine(Resources.TelemetryJustification_Message);
                Console.WriteLine(Resources.TelemetryAnonymous_Message);
                UserSettings.TelemetryDisable = !Prompt.Confirm(Resources.EnableTelemetryFirstRun_Message);
            }
        }

        private static bool IsRunningAsUwp()
        {
            DesktopBridge.Helpers helpers = new DesktopBridge.Helpers();
            return helpers.IsRunningAsUwp();
        }
    }
}
