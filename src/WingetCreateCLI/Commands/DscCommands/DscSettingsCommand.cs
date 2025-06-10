// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using System;
using Microsoft.WingetCreateCLI.Models.Settings;
using Newtonsoft.Json.Linq;

/// <summary>
/// Command for managing the settings using dsc v3.
/// </summary>
public class DscSettingsCommand : BaseDscCommand
{
    /// <summary>
    /// Represents the name of the command used to access settings functionality.
    /// </summary>
    public const string CommandName = "settings";

    /// <inheritdoc/>
    public override void Get(JToken input)
    {
        Console.WriteLine(UserSettings.ToJson());
    }

    /// <inheritdoc/>
    public override void Set(JToken input)
    {
        // No-op
    }

    /// <inheritdoc/>
    public override void Test(JToken input)
    {
        // No-op
    }

    /// <inheritdoc/>
    public override void Export(JToken input)
    {
        // No-op
    }

    private class UserSettingsFunctionData
    {
        public enum ActionType
        {
            Partial,
            Full,
        }

        public UserSettingsFunctionData(JToken token)
        {

        }

        /// <summary>
        /// Gets or sets the action type for the settings command.
        /// </summary>
        public ActionType Action { get; set; } = ActionType.Partial;

        /// <summary>
        /// Gets or sets the settings manifest to be used for the command.
        /// </summary>
        public SettingsManifest Settings { get; set; }
    }
}
