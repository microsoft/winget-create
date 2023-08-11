// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using Windows.Storage;

    /// <summary>
    /// Helper class containing common functionality for the CLI.
    /// </summary>
    public static class Common
    {
        private const string ModuleName = "WindowsPackageManagerManifestCreator";

        private static readonly Lazy<string> AppStatePathLazy = new(() =>
        {
            string path = IsRunningAsUwp()
                ? ApplicationData.Current.LocalFolder.Path
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", ModuleName);
            Directory.CreateDirectory(path);
            return path;
        });

        /// <summary>
        /// Gets directory path where app should store local state.
        /// </summary>
        public static string LocalAppStatePath => AppStatePathLazy.Value;

        /// <summary>
        /// Initiates the clean up process for the given directory.
        /// </summary>
        /// <param name="cleanUpDirectory">Directory to clean up.</param>
        public static void BeginCleanUp(string cleanUpDirectory)
        {
            if (UserSettings.CleanUpDisabled)
            {
                return;
            }

            var logDirectory = new DirectoryInfo(cleanUpDirectory);
            var files = logDirectory.GetFiles();
            foreach (var file in files)
            {
                if (file.CreationTime < DateTime.Now.AddDays(-UserSettings.CleanUpDays))
                {
                    file.Delete();
                }
            }

            var directories = logDirectory.GetDirectories();
            foreach (var directory in directories)
            {
                if (directory.CreationTime < DateTime.Now.AddDays(-UserSettings.CleanUpDays))
                {
                    directory.Delete(true);
                }
            }
        }

        private static bool IsRunningAsUwp()
        {
            DesktopBridge.Helpers helpers = new DesktopBridge.Helpers();
            return helpers.IsRunningAsUwp();
        }
    }
}
