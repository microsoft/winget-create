// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using Microsoft.Diagnostics.Telemetry.Internal;

    /// <summary>
    /// Interface for telemetry event objects.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// Gets the privacy datatype tag.
        /// </summary>
        public PartA_PrivTags PartA_PrivTags { get; }
    }
}
