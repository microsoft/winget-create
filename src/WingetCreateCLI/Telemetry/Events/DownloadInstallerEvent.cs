// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Diagnostics.Tracing;

    /// <summary>
    /// Telemetry event for an event that successfully downloads an installer.
    /// </summary>
    [EventData]
    public class DownloadInstallerEvent : GlobalExceptionEvent
    {
    }
}
