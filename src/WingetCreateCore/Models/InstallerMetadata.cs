// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using System.Collections.Generic;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// Helper class for storing an installer's metadata information.
    /// </summary>
    public class InstallerMetadata
    {
        /// <summary>
        /// Gets or sets the installer url.
        /// </summary>
        public string InstallerUrl { get; set; }

        /// <summary>
        /// Gets or sets the path to the package to extract metadata from.
        /// </summary>
        public string PackageFile { get; set; }

        /// <summary>
        /// Gets or sets the new installers for updating the manifest.
        /// </summary>
        public List<Installer.Installer> NewInstallers { get; set; } = new List<Installer.Installer>();

        /// <summary>
        /// Gets or sets the architecture detected from the URL string.
        /// </summary>
        public InstallerArchitecture? UrlArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture detected from the binary.
        /// </summary>
        public InstallerArchitecture? BinaryArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture specified as an override.
        /// </summary>
        public InstallerArchitecture? OverrideArchitecture { get; set; }
    }
}
