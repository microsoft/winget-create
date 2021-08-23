// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when the download size of the installer exceeds the max download size.
    /// </summary>
    public class DownloadSizeExceededException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadSizeExceededException"/> class.
        /// </summary>
        /// <param name="maxDownloadSizeInBytes">The maximum file size in bytes.</param>
        public DownloadSizeExceededException(long maxDownloadSizeInBytes)
        {
            this.MaxDownloadSizeInBytes = maxDownloadSizeInBytes;
        }

        /// <summary>
        /// Gets the maximum file size in bytes.
        /// </summary>
        public long MaxDownloadSizeInBytes { get; private set; }
    }
}
