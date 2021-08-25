// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;
    using System.Collections.Generic;

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
