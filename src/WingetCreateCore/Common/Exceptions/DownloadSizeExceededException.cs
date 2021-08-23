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
}
