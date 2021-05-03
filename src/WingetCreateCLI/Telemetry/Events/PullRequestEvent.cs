// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Telemetry.Internal;

    /// <summary>
    /// Telemetry event for a pull request event.
    /// </summary>
    [EventData]
    public class PullRequestEvent : GlobalExceptionEvent
    {
        /// <summary>
        /// Gets or sets the generated pull request url.
        /// </summary>
        public int PullRequestNumber { get; set; }

        /// <inheritdoc/>
        public override PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage | PartA_PrivTags.ProductAndServicePerformance;
    }
}
