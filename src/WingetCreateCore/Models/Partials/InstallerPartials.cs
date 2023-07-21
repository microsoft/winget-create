// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.Installer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using Microsoft.WingetCreateCore.Models.CustomValidation;
    using Newtonsoft.Json;
    using YamlDotNet.Serialization;

    /// <summary>
    /// Partial class that implements helper methods for the PackageDependencies class.
    /// </summary>
    public partial class PackageDependencies : ICloneable
    {
        /// <summary>
        /// Creates a new PackageDependencies object that is a copy of the current instance.
        /// </summary>
        /// <returns>A new PackageDependencies object that is a copy of the current instance.</returns>
        public object Clone()
        {
            return new PackageDependencies { MinimumVersion = this.MinimumVersion, PackageIdentifier = this.PackageIdentifier };
        }

        /// <summary>
        /// Gives the criteria for determining whether two instances of PackageDependencies objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            PackageDependencies other = (PackageDependencies)obj;
            return this.PackageIdentifier == other.PackageIdentifier &&
                this.MinimumVersion == other.MinimumVersion;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the InstallerSwitches class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class InstallerSwitches
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of InstallerSwitches objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            InstallerSwitches other = (InstallerSwitches)obj;
            return this.Silent == other.Silent &&
                this.SilentWithProgress == other.SilentWithProgress &&
                this.Interactive == other.Interactive &&
                this.InstallLocation == other.InstallLocation &&
                this.Log == other.Log &&
                this.Upgrade == other.Upgrade &&
                this.Custom == other.Custom;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the Dependencies class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Dependencies
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of Dependencies objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            Dependencies other = (Dependencies)obj;
            return this.WindowsFeatures.ToList().SequenceEqual(other.WindowsFeatures) &&
                this.WindowsLibraries.ToList().SequenceEqual(this.WindowsLibraries) &&
                this.PackageDependencies.ToList().SequenceEqual(other.PackageDependencies) &&
                this.ExternalDependencies.ToList().SequenceEqual(other.ExternalDependencies);
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the AppsAndFeaturesEntry class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class AppsAndFeaturesEntry
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of AppsAndFeaturesEntry objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            AppsAndFeaturesEntry other = (AppsAndFeaturesEntry)obj;
            return this.DisplayName == other.DisplayName &&
                this.Publisher == other.Publisher &&
                this.DisplayVersion == other.DisplayVersion &&
                this.ProductCode == other.ProductCode &&
                this.UpgradeCode == other.UpgradeCode &&
                this.InstallerType == other.InstallerType;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the InstallationMetadata class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class InstallationMetadata
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of InstallationMetadata objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            InstallationMetadata other = (InstallationMetadata)obj;
            return this.DefaultInstallLocation == other.DefaultInstallLocation &&
                this.Files.ToList().SequenceEqual(other.Files);
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the Files class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Files
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of Files objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            Files other = (Files)obj;
            return this.RelativeFilePath == other.RelativeFilePath &&
                this.FileSha256 == other.FileSha256 &&
                this.FileType == other.FileType &&
                this.InvocationParameter == other.InvocationParameter &&
                this.DisplayName == other.DisplayName;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the NestedInstallerFile class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class NestedInstallerFile
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of NestedInstallerFile objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            NestedInstallerFile other = (NestedInstallerFile)obj;
            return this.RelativeFilePath == other.RelativeFilePath &&
                this.PortableCommandAlias == other.PortableCommandAlias;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the ExpectedReturnCode class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class ExpectedReturnCode
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of ExpectedReturnCode objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            ExpectedReturnCode other = (ExpectedReturnCode)obj;
            return this.InstallerReturnCode == other.InstallerReturnCode &&
                this.ReturnResponse == other.ReturnResponse &&
                this.ReturnResponseUrl == other.ReturnResponseUrl;
        }
    }

    /// <summary>
    /// Partial InstallerManifest class for defining a string type ReleaseDateTimeField.
    /// Workaround for issue with model generating ReleaseDate with DateTimeOffset type.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class InstallerManifest
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gets or sets the Release Date time.
        /// </summary>
        [YamlMember(Alias = "ReleaseDate")]
        [DateTimeValidation]
        public string ReleaseDateTime { get; set; }
    }

    /// <summary>
    /// Partial Installer class for defining a string type ReleaseDateTime field.
    /// Workaround for issue with model generating ReleaseDate with DateTimeOffset type.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Installer
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gets or sets the Release Date time.
        /// </summary>
        [YamlMember(Alias = "ReleaseDate")]
        [DateTimeValidation]
        public string ReleaseDateTime { get; set; }
    }

    /// <summary>
    /// This partial class is a workaround to a known issue.
    /// NSwag does not handle the "oneof" keyword of the Markets field, resulting in missing model classes.
    /// Because of this, the Markets and Markets array classes do not get generated, therefore we need to define them here
    /// under the default NSwag generated name "Markets2".
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Markets2
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gets or sets the list of allowed installer target markets.
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public List<string> AllowedMarkets { get; set; }

        /// <summary>
        /// Gets or sets the list of excluded installer target markets.
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public List<string> ExcludedMarkets { get; set; }

        // TO DO: Implement Equals() override when the model is updated to include the schema properties. Update MoveInstallerFieldsToRoot and DontMoveInstallerFieldsToRoot test cases to verify the equality.
    }
}
