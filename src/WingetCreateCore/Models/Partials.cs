// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.Installer
{
    using System;

    /// <summary>
    /// Partial class that implements cloning functionality to the PackageDependencies class.
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
    }
}
