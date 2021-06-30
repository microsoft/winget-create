// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Telemetry.Events
{
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Telemetry.Internal;

    /// <summary>
    /// Telemetry event for executing the command.
    /// </summary>
    [EventData]
    public class CommandExecutedEvent : EventBase
    {
        /// <summary>
        /// Gets or sets the executed command.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a GitHub token was provided.
        /// </summary>
        public bool HasGitHubToken { get; set; }

        /// <summary>
        /// Gets or sets the user provided installer URL(s).
        /// </summary>
        public string InstallerUrl { get; set; }

        /// <summary>
        /// Gets or sets the user provided package ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the user provided version.
        /// </summary>
        public string Version { get; set; }

        /// <inheritdoc/>
        public override PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
