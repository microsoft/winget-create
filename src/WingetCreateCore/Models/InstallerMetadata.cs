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
        public Architecture? UrlArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture detected from the binary.
        /// </summary>
        public Architecture? BinaryArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the architecture specified as an override.
        /// </summary>
        public Architecture? OverrideArchitecture { get; set; }

        /// <summary>
        /// Gets or sets the scope specified as an override.
        /// </summary>
        public Scope? OverrideScope { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the installer came from a zip.
        /// </summary>
        public bool IsZipFile { get; set; } = false;

        /// <summary>
        /// Gets or sets the nested installer files contained inside a zip.
        /// </summary>
        public List<string> RelativeFilePaths { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the nested installers in a zip have multiple architectures.
        /// </summary>
        public bool MultipleNestedInstallerArchitectures { get; set; }

        /// <summary>
        /// Gets or sets the directory path of the extracted files from the target zip package file.
        /// </summary>
        public string ExtractedDirectory { get; set; }
    }
}
