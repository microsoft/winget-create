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
    }
}
