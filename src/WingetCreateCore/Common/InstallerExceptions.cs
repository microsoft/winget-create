// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// Exception class for handling installer matching errors.
    /// </summary>
    public class InstallerExceptions
    {
        /// <summary>
        /// The exception that is thrown when an installer cannot be matched to any single existing installer.
        /// </summary>
        public class UnmatchedInstallerException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="UnmatchedInstallerException"/> class.
            /// </summary>
            /// <param name="unmatchedInstaller">The installer with no matches.</param>
            public UnmatchedInstallerException(Installer unmatchedInstaller)
            {
                this.UnmatchedInstaller = unmatchedInstaller;
            }

            /// <summary>
            /// Gets the unmatched installer.
            /// </summary>
            public Installer UnmatchedInstaller { get; private set; }
        }

        /// <summary>
        /// The exception that is thrown when an installer matches multiple existing installers.
        /// </summary>
        public class MultipleMatchedInstallerException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MultipleMatchedInstallerException"/> class.
            /// </summary>
            /// <param name="multipleMatchedInstaller">The installer with multiple matches.</param>
            public MultipleMatchedInstallerException(Installer multipleMatchedInstaller)
            {
                this.MultipleMatchedInstaller = multipleMatchedInstaller;
            }

            /// <summary>
            /// Gets the installer with multiple matches.
            /// </summary>
            public Installer MultipleMatchedInstaller { get; private set; }
        }
    }
}
