// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;

    /// <summary>
    /// Wrapper class for the manifest object models.
    /// </summary>
    public class Manifests
    {
        /// <summary>
        /// Gets or sets the singleton manifest object model.
        /// </summary>
        public SingletonManifest SingletonManifest { get; set; }

        /// <summary>
        /// Gets or sets the version manifest object model.
        /// </summary>
        public VersionManifest VersionManifest { get; set; }

        /// <summary>
        /// Gets or sets the installer manifest object model.
        /// </summary>
        public InstallerManifest InstallerManifest { get; set; }

        /// <summary>
        /// Gets or sets the default locale manifest object model.
        /// </summary>
        public DefaultLocaleManifest DefaultLocaleManifest { get; set; }

        /// <summary>
        /// Gets or sets the list of locale manifest object models.
        /// </summary>
        public List<LocaleManifest> LocaleManifests { get; set; } = new List<LocaleManifest>();

        /// <summary>
        /// Generates the proper file name for the given manifest.
        /// </summary>
        /// <typeparam name="T">Manifest type.</typeparam>
        /// <param name="manifest">Manifest object model.</param>
        /// <param name="extension">File extension to use depending on the serialization format.</param>
        /// <returns>File name string of manifest.</returns>
        public static string GetFileName<T>(T manifest, string extension)
        {
            return manifest switch
            {
                InstallerManifest installerManifest => $"{installerManifest.PackageIdentifier}.installer{extension}",
                VersionManifest versionManifest => $"{versionManifest.PackageIdentifier}{extension}",
                DefaultLocaleManifest defaultLocaleManifest => $"{defaultLocaleManifest.PackageIdentifier}.locale.{defaultLocaleManifest.PackageLocale}{extension}",
                LocaleManifest localeManifest => $"{localeManifest.PackageIdentifier}.locale.{localeManifest.PackageLocale}{extension}",
                SingletonManifest singletonManifest => $"{singletonManifest.PackageIdentifier}{extension}",
                _ => throw new ArgumentException(nameof(manifest)),
            };
        }

        /// <summary>
        /// Creates a new cloned list of Installers that is a copy of the current list of installers.
        /// </summary>
        /// <returns>A new cloned list of Installers.</returns>
        public List<Installer.Installer> CloneInstallers()
        {
            List<Installer.Installer> deepCopy = new List<Installer.Installer>();
            this.InstallerManifest.Installers.ForEach(i => deepCopy.Add(PackageParser.CloneInstaller(i)));
            return deepCopy;
        }
    }
}
