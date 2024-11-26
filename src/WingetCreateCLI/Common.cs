// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
#if WINDOWS
    using Windows.Storage;
#endif

    /// <summary>
    /// Helper class containing common functionality for the CLI.
    /// </summary>
    public static class Common
    {
        private const string ModuleName = "WindowsPackageManagerManifestCreator";
        private const string UserProfileEnvironmentVariable = "%USERPROFILE%";
        private const string UserHomeDirectoryShortcutUnix = "~";
        private const string LocalAppDataEnvironmentVariable = "%LOCALAPPDATA%";
        private const string TempEnvironmentVariable = "%TEMP%";

        private static readonly Lazy<string> AppStatePathLazy = new(() =>
        {
#if WINDOWS
            string path = IsRunningAsUwp()
                ? ApplicationData.Current.LocalFolder.Path
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", ModuleName);
#else
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wingetcreate");
#endif
            Directory.CreateDirectory(path);
            return path;
        });

        /// <summary>
        /// Gets directory path where app should store local state.
        /// </summary>
        public static string LocalAppStatePath => AppStatePathLazy.Value;

        /// <summary>
        /// Cleans up files and folders in a specified directory that are older than the specified number of days.
        /// </summary>
        /// <param name="cleanUpDirectory">Directory to clean up.</param>
        /// <param name="cleanUpDays">The number of days that determine the age of files to be considered for cleanup.</param>
        public static void CleanUpFilesOlderThan(string cleanUpDirectory, int cleanUpDays)
        {
            var directory = new DirectoryInfo(cleanUpDirectory);

            if (!directory.Exists)
            {
                return;
            }

            var files = directory.GetFiles();
            foreach (var file in files)
            {
                if (file.CreationTime < DateTime.Now.AddDays(-cleanUpDays))
                {
                    file.Delete();
                }
            }

            var directories = directory.GetDirectories();
            foreach (var subDirectory in directories)
            {
                if (subDirectory.CreationTime < DateTime.Now.AddDays(-cleanUpDays))
                {
                    subDirectory.Delete(true);
                }
            }
        }

        /// <summary>
        /// Gets the path for display. This will anonymize the path if caller provides the appropriate flag.
        /// </summary>
        /// <param name="path">Path to be displayed.</param>
        /// <param name="substituteEnvironmentVariables">Whether or not to substitute environment variables.</param>
        /// <returns>Anonymized path or original path.</returns>
        public static string GetPathForDisplay(string path, bool substituteEnvironmentVariables = true)
        {
            if (string.IsNullOrEmpty(path) || !substituteEnvironmentVariables)
            {
                return path;
            }

            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (path.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Replace(tempPath, TempEnvironmentVariable, StringComparison.OrdinalIgnoreCase);
                }
                else if (path.StartsWith(localAppDataPath, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Replace(localAppDataPath, LocalAppDataEnvironmentVariable, StringComparison.OrdinalIgnoreCase);
                }
                else if (path.StartsWith(userProfilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Replace(userProfilePath, UserProfileEnvironmentVariable, StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                // Paths are case-sensitive on Unix
                if (path.StartsWith(userProfilePath, StringComparison.Ordinal))
                {
                    return path.Replace(userProfilePath, UserHomeDirectoryShortcutUnix, StringComparison.Ordinal);
                }
            }

            return path;
        }

#if WINDOWS
        private static bool IsRunningAsUwp()
        {
            DesktopBridge.Helpers helpers = new DesktopBridge.Helpers();
            return helpers.IsRunningAsUwp();
        }
#endif
    }
}
