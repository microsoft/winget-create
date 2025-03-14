// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when the forked repo's branch has a merge conflict with the upstream branch.
    /// </summary>
    public class BranchMergeConflictException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BranchMergeConflictException"/> class.
        /// </summary>
        public BranchMergeConflictException()
        {
        }
    }
}
