// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Telemetry.Internal;

    /// <summary>
    /// Telemetry event for an event that successfully downloads an installer.
    /// </summary>
    [EventData]
    public class GlobalExceptionEvent : EventBase
    {
        /// <summary>
        /// Gets or sets the type of the exception being thrown.
        /// </summary>
        public string ExceptionType { get; set; }

        /// <summary>
        /// Gets or sets the error message associated with the failed download.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the stack trace of the exception.
        /// </summary>
        public string StackTrace { get; set; }

        /// <inheritdoc/>
        public override PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
