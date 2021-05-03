// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System.Collections.Generic;

    /// <summary>
    /// Wrapper class for utilizing WingetUtil.dll version comparer.
    /// </summary>
    public class VersionComparer : IComparer<string>
    {
        /// <summary>
        /// Compares versions using WinGetUtil.dll.
        /// </summary>
        /// <param name="versionA">The first version string to compare, or null.</param>
        /// <param name="versionB">The second version string to compare, or null.</param>
        /// <returns>Int representing the result of the version comparison.</returns>
        public int Compare(string versionA, string versionB)
        {
            return WinGetUtil.CompareVersions(versionA, versionB);
        }
    }
}
