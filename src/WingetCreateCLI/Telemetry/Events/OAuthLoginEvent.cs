// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Telemetry.Internal;

    /// <summary>
    /// Telemetry event for a GitHub OAuth login request.
    /// </summary>
    [EventData]
    public class OAuthLoginEvent : EventBase
    {
        /// <summary>
        /// Gets or sets the error associated with the failed GitHub login attempt.
        /// </summary>
        public string Error { get; set; }

        /// <inheritdoc/>
        public override PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
