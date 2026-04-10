// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.IO;

    /// <summary>
    /// Helper class for common utility functions.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Gets the version of the process executable.
        /// </summary>
        /// <returns>Version string of the process executable. </returns>
        public static string GetEntryAssemblyVersion()
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            return assembly.GetName().Version.ToString();
        }

        /// <summary>
        /// Gets the name of the process executable.
        /// </summary>
        /// <returns>Name string of the process executable. </returns>
        public static string GetEntryAssemblyName()
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            return assembly.GetName().Name;
        }

        /// <summary>
        /// Generates the full app manifest directory path based on the formatted directory structure of the winget-pkgs repo.
        /// Example: manifests\m\Microsoft\PowerToys\{version}.
        /// </summary>
        /// <param name="packageId">Package Identifier.</param>
        /// <param name="version">Package Version.</param>
        /// <param name="manifestRoot">The manifest root name.</param>
        /// <param name="pathDelimiter">Delimiter character of the generated path.</param>
        /// <returns>Full directory path where the manifests should be saved to.</returns>
        public static string GetAppManifestDirPath(string packageId, string version, string manifestRoot = Constants.WingetManifestRoot, char pathDelimiter = '\\')
        {
            string path = Path.Combine(manifestRoot, $"{char.ToLowerInvariant(packageId[0])}", packageId.Replace('.', '\\'), version);
            return pathDelimiter != '\\' ? path.Replace('\\', pathDelimiter) : path;
        }

        /// <summary>
        /// Writes a formatted string to the console in a specifed console color.
        /// </summary>
        /// <param name="color">Console color of the outputted string. </param>
        /// <param name="format">String with format items.</param>
        /// <param name="args">Arguments used to replace the format item(s) with the string representation of the arguments. </param>
        public static void WriteLineColored(ConsoleColor color, string format, params object[] args)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(string.Format(format, args));
            Console.ResetColor();
        }
    }
}
