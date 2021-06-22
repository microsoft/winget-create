// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry
{
    using System.Diagnostics.Tracing;
    using Microsoft.WingetCreateCLI.Commands;

    /// <summary>
    /// Telemetry Event Listener class for WingetCreate
    /// </summary>
    public class TelemetryEventListener : EventListener
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryEventListener"/> class
        /// </summary>
        public TelemetryEventListener()
            : base()
        {
        }

        /// <summary>
        /// Gets the static instance of the <see cref="TelemetryEventListener"/> class.
        /// </summary>
        public static TelemetryEventListener EventListener { get; } = new TelemetryEventListener();

        /// <summary>
        /// Checks if the Telemetry field of the settings file is and toggles telemetry based on that value
        /// </summary>
        public void IsTelemetryEnabled()
        {
            if (!SettingsCommand.SettingsManifest.Telemetry.Disable)
            {
                this.DisableEvents(new EventSource("Microsoft.PackageManager.Create"));
            }
        }
    }
}
