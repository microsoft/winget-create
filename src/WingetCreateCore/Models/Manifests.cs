// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
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

        public List<Installer.Installer> CloneInstallers()
        {
            List<Installer.Installer> deepCopy = new List<Installer.Installer>();

            this.InstallerManifest.Installers.ForEach(
                i => deepCopy.Add(
                    new Installer.Installer {
                        InstallerLocale = i.InstallerLocale,
                        Platform = i.Platform,
                        MinimumOSVersion = i.MinimumOSVersion,
                        Architecture = i.Architecture,
                        InstallerType = i.InstallerType,
                        Scope = i.Scope,
                        InstallerUrl = i.InstallerUrl,
                        InstallerSha256 = i.InstallerSha256,
                        SignatureSha256 = i.SignatureSha256,
                        InstallModes = i.InstallModes,
                        InstallerSwitches = i.InstallerSwitches,
                        InstallerSuccessCodes = i.InstallerSuccessCodes,
                        UpgradeBehavior = i.UpgradeBehavior,
                        Commands = i.Commands,
                        Protocols = i.Protocols,
                        FileExtensions = i.FileExtensions,
                        PackageFamilyName = i.PackageFamilyName,
                        ProductCode = i.ProductCode,
                        Capabilities = i.Capabilities,
                        RestrictedCapabilities = i.RestrictedCapabilities,
                    }));

            return deepCopy;
        } 
    }
}
