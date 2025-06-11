// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using System;
using System.Diagnostics;
using Microsoft.WingetCreateCLI.Models.Settings;
using Newtonsoft.Json;
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
        this.Export(input);
    }

    /// <inheritdoc/>
    public override void Set(JToken input)
    {
        var data = new UserSettingsFunctionData(input);
        data.Get();

        // Capture the diff before updating the output
        var diff = data.DiffJson();

        if (!data.Test())
        {
            data.Output.Settings = data.GetResolvedInput();
            data.WriteOutput();
        }

        this.WriteJsonOutputLine(data.Output.ToJson());
        this.WriteJsonOutputLine(diff);
    }

    /// <inheritdoc/>
    public override void Test(JToken input)
    {
        var data = new UserSettingsFunctionData(input);

        data.Get();
        data.Output.InDesiredState = data.Test();

        this.WriteJsonOutputLine(data.Output.ToJson());
        this.WriteJsonOutputLine(data.DiffJson());
    }

    /// <inheritdoc/>
    public override void Export(JToken input)
    {
        var data = new UserSettingsFunctionData(input);

        data.Get();

        this.WriteJsonOutputLine(data.Output.ToJson());
    }

    private class UserSettingsFunctionData
    {
        private JObject resolvedInputUserSettings;
        private JObject userSettings;

        public UserSettingsFunctionData(JToken json = null)
        {
            this.Input = json == null ? new() : json.ToObject<UserSettingsResourceObject>();
            this.Output = new();
        }

        public UserSettingsResourceObject Input { get; }

        public UserSettingsResourceObject Output { get; }

        public void Get()
        {
            this.Output.Settings = this.GetUserSettings();
        }

        public bool Test()
        {
            return JToken.DeepEquals(this.GetResolvedInput(), this.GetValidSettings(this.Output.Settings));
        }

        public JArray DiffJson()
        {
            var diff = new JArray();
            if (!this.Test())
            {
                diff.Add("settings");
            }

            return diff;
        }

        public JObject GetResolvedInput()
        {
            Debug.Assert(this.Input.Settings != null, "Input settings should not be null.");
            if (this.resolvedInputUserSettings == null)
            {
                if (UserSettingsResourceObject.ActionFull.Equals(this.Input.Action, StringComparison.OrdinalIgnoreCase))
                {
                    this.Output.Action = UserSettingsResourceObject.ActionFull;
                    this.resolvedInputUserSettings = this.GetValidSettings(this.Input.Settings);
                }
                else
                {
                    this.Output.Action = UserSettingsResourceObject.ActionPartial;
                    var mergedSettings = this.GetUserSettings();
                    mergedSettings.Merge(this.Input.Settings);
                    this.resolvedInputUserSettings = this.GetValidSettings(mergedSettings);
                }
            }

            return this.resolvedInputUserSettings;
        }

        public JObject GetUserSettings()
        {
            this.userSettings ??= UserSettings.ToJson();
            return (JObject)this.userSettings.DeepClone();
        }

        public void WriteOutput()
        {
            Debug.Assert(this.Output.Settings != null, "Output settings should not be null.");
            UserSettings.SaveSettings(this.Output.Settings.ToObject<SettingsManifest>());
        }

        /// <summary>
        /// Validates and converts the provided settings into a structured format.
        /// </summary>
        /// <param name="settings">An object containing settings to be validated.</param>
        /// <returns>An object representing the validated settings.</returns>
        public JObject GetValidSettings(JObject settings)
        {
            var settingsManifest = settings.ToObject<SettingsManifest>();
            return JObject.FromObject(settingsManifest);
        }
    }

    private class UserSettingsResourceObject
    {
        public const string ActionFull = "Full";
        public const string ActionPartial = "Partial";

        // TODO Make this required
        [JsonProperty("settings")]
        public JObject Settings { get; set; }

        [JsonProperty("action", NullValueHandling = NullValueHandling.Ignore)]
        public string Action { get; set; }

        [JsonProperty("_inDesiredState", NullValueHandling = NullValueHandling.Ignore)]
        public bool? InDesiredState { get; set; }

        /// <summary>
        /// Converts the current object to a JSON representation.
        /// </summary>
        /// <returns>A Json object representing the current object.</returns>
        public JObject ToJson()
        {
            return JObject.FromObject(this, new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
            });
        }
    }
}
