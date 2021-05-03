// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry
{
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;

    /// <summary>
    /// Telemetry helper class for WingetCreate.
    /// </summary>
    public class TelemetryManager : TelemetryEventSource
    {
        /// <summary>
        /// Name for ETW event.
        /// </summary>
        private const string EventSourceName = "Microsoft.PackageManager.Create";

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryManager"/> class.
        /// </summary>
        public TelemetryManager()
            : base(EventSourceName, TelemetryGroup.MicrosoftTelemetry)
        {
        }

        /// <summary>
        /// Gets an instance of the <see cref="TelemetryManager"/> class.
        /// </summary>
        public static TelemetryManager Log { get; } = new TelemetryManager();

        /// <summary>
        /// Publishes ETW event when an action is triggered on.
        /// </summary>
        /// <typeparam name="T">Telemetry event type.</typeparam>
        /// <param name="telemetryEvent">Telemetry event data object.</param>
        public void WriteEvent<T>(T telemetryEvent)
            where T : EventBase
        {
            this.Write<T>(
                null,
                new EventSourceOptions()
                {
                    Keywords = CriticalDataKeyword,
                },
                telemetryEvent);

            EventBase.IncrementCorrelationVector();
        }
    }
}
