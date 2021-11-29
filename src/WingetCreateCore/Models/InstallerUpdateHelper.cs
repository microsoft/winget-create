// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using System.Collections.Generic;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// Helper class for storing information about new installers used during the update flow.
    /// </summary>
    public class InstallerUpdateHelper
    {
        /// <summary>
        /// Gets or sets a list of new installer urls for updating the manifest.
        /// </summary>
        public IEnumerable<string> InstallerUrls { get; set; }

        /// <summary>
        /// Gets or sets a list of paths to packages to extract metadata from.
        /// </summary>
        public IList<string> PackageFiles { get; set; }

        /// <summary>
        /// Gets or sets a list of DetectedArch object models that represent each installers detected architectures.
        /// </summary>
        public List<PackageParser.DetectedArch> DetectedArchitectures { get; set; }

        /// <summary>
        /// Gets or sets a list of new installers for updating the manifest.
        /// </summary>
        public List<Installer.Installer> NewInstallers { get; set; }

        /// <summary>
        /// Gets or sets a dictionary that maps the installer URL with the override architecture.
        /// </summary>
        public Dictionary<string, InstallerArchitecture> ArchitectureOverrideMap { get; set; }
    }
}
