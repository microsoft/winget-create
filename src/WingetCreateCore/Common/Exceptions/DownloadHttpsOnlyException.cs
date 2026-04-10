// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when the download URL is not HTTPS.
    /// </summary>
    public class DownloadHttpsOnlyException : Exception
    {
    }
}
