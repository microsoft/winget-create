// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// The exception that is thrown when new installers fail to match existing installers.
    /// </summary>
    public class InstallerMatchException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallerMatchException"/> class.
        /// </summary>
        /// <param name="multipleMatchedInstaller">List of installers with multiple matches.</param>
        /// <param name="unmatchedInstallers">List of installers with no matches.</param>
        /// <param name="isArchitectureOverride">Architecture of an installer was overridden by the user.</param>
        public InstallerMatchException(List<Installer> multipleMatchedInstaller, List<Installer> unmatchedInstallers, bool isArchitectureOverride)
        {
            this.MultipleMatchedInstallers = multipleMatchedInstaller;
            this.UnmatchedInstallers = unmatchedInstallers;
            this.IsArchitectureOverride = isArchitectureOverride;
        }

        /// <summary>
        /// Gets the list of installers with multiple matches.
        /// </summary>
        public List<Installer> MultipleMatchedInstallers { get; private set; }

        /// <summary>
        /// Gets the list of installers with no matches.
        /// </summary>
        public List<Installer> UnmatchedInstallers { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the user overrode the architecture of installer to be updated.
        /// </summary>
        public bool IsArchitectureOverride { get; private set; }
    }
}
