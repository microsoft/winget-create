// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when attemping a non-fast forward update to a GitHub repository branch.
    /// </summary>
    public class NonFastForwardException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NonFastForwardException"/> class.
        /// </summary>
        /// <param name="commitsAheadBy">The number of commits the branch is ahead by.</param>
        public NonFastForwardException(int commitsAheadBy)
        {
            this.CommitsAheadBy = commitsAheadBy;
        }

        /// <summary>
        /// Gets the number of commits the branch is ahead by.
        /// </summary>
        public int CommitsAheadBy { get; private set; }
    }
}
