// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Diagnostics.Tracing;
    using Microsoft.CorrelationVector;
    using Microsoft.Diagnostics.Telemetry.Internal;
    using Microsoft.WingetCreateCore.Common;

    /// <summary>
    /// A base class to implement properties that are common to all telemetry events.
    /// </summary>
    [EventData]
    public abstract class EventBase
    {
        private static readonly CorrelationVector CorrelationVector = new CorrelationVector(CorrelationVectorVersion.V2);

        private string version;

        /// <summary>
        /// Gets a value indicating whether to replace the UTC app session GUID.
        /// </summary>
        public bool UTCReplace_AppSessionGuid => true;

        /// <summary>
        /// Gets the app version from the assembly.
        /// </summary>
        public string AppVersion
        {
            get
            {
                if (string.IsNullOrEmpty(this.version))
                {
                    this.version = Utils.GetEntryAssemblyVersion();
                }

                return this.version;
            }
        }

        /// <summary>
        /// Gets the correlation vector value associated with the event.
        /// </summary>
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles
        public string __TlgCV__ => CorrelationVector.Value;
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// Gets or sets a value indicating whether the event was successful.
        /// </summary>
        public bool IsSuccessful { get; set; } = false;

        /// <summary>
        /// Gets the privacy datatype tag for the telemetry event.
        /// </summary>
        public abstract PartA_PrivTags PartA_PrivTags { get; }

        /// <summary>
        /// Increments the correlation vector extension value.
        /// </summary>
        public static void IncrementCorrelationVector() => CorrelationVector.Increment();
    }
}
