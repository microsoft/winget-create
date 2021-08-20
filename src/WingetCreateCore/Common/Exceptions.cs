// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common
{
    using System;
    using System.Collections.Generic;
    using Microsoft.WingetCreateCore.Models.Installer;

    /// <summary>
    /// Exception class for handling installer matching errors.
    /// </summary>
    public class Exceptions
    {
        /// <summary>
        /// The exception that is thrown when the download size of the installer exceeds the max download size.
        /// </summary>
        public class DownloadSizeExceededException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="DownloadSizeExceededException"/> class.
            /// </summary>
            /// <param name="maxDownloadSize">The maximum file size in bytes.</param>
            public DownloadSizeExceededException(long? maxDownloadSize)
            {
                this.MaxDownloadSize = maxDownloadSize;
            }

            /// <summary>
            /// Gets the maximum file size in bytes.
            /// </summary>
            public long? MaxDownloadSize { get; private set; }
        }

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
            public InstallerMatchException(List<Installer> multipleMatchedInstaller, List<Installer> unmatchedInstallers)
            {
                this.MultipleMatchedInstallers = multipleMatchedInstaller;
                this.UnmatchedInstallers = unmatchedInstallers;
            }

            /// <summary>
            /// Gets the list of installers with multiple matches.
            /// </summary>
            public List<Installer> MultipleMatchedInstallers { get; private set; }

            /// <summary>
            /// Gets the list of installers with no matches.
            /// </summary>
            public List<Installer> UnmatchedInstallers { get; private set; }
        }

        /// <summary>
        /// The exception that is thrown when installers fail to parse.
        /// </summary>
        public class ParsePackageException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ParsePackageException"/> class.
            /// </summary>
            /// <param name="parseFailedInstallerUrls">List of installer urls that failed to parse.</param>
            public ParsePackageException(List<string> parseFailedInstallerUrls)
            {
                this.ParseFailedInstallerUrls = parseFailedInstallerUrls;
            }

            /// <summary>
            /// Gets a list of installer urls that failed to parse.
            /// </summary>
            public List<string> ParseFailedInstallerUrls { get; private set; }
        }
    }
}
