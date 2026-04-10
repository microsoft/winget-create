// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when a generic failure occurs while syncing the forked repo with upstream commits.
    /// </summary>
    public class GenericSyncFailureException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenericSyncFailureException"/> class.
        /// </summary>
        public GenericSyncFailureException()
        {
        }
    }
}
