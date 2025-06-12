// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

using System;
using System.Diagnostics;
using Microsoft.WingetCreateCLI.Models.Settings;
using Newtonsoft.Json.Linq;

/// <summary>
/// Represents the data structure for settings functionality in DSC operations.
/// </summary>
public class SettingsFunctionData
{
    private JObject resolvedInputUserSettings;
    private JObject userSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsFunctionData"/> class with default settings.
    /// </summary>
    public SettingsFunctionData()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsFunctionData"/> class.
    /// </summary>
    /// <param name="json">An optional JSON token used to initialize the input settings.</param>
    public SettingsFunctionData(JToken json = null)
    {
        this.Output = new();
        this.Input = json == null ? new() : json.ToObject<SettingsResourceObject>();
    }

    /// <summary>
    /// Gets the input settings resource object.
    /// </summary>
    public SettingsResourceObject Input { get; }

    /// <summary>
    /// Gets the output settings resource object.
    /// </summary>
    public SettingsResourceObject Output { get; }

    /// <summary>
    /// Loads the user settings into the output object.
    /// </summary>
    public void Get()
    {
        this.Output.Settings = this.GetUserSettings();
    }

    /// <summary>
    /// Gets whether the resolved input settings and output settings are equivalent.
    /// </summary>
    /// <returns>>true if the settings are equivalent; otherwise, false.</returns>
    public bool Test()
    {
        return JToken.DeepEquals(this.GetResolvedInput(), this.GetValidSettings(this.Output.Settings));
    }

    /// <summary>
    /// Gets the differences between the current settings and the input settings.
    /// </summary>
    /// <returns>A Json array containing the differences.</returns>
    public JArray DiffJson()
    {
        var diff = new JArray();
        if (!this.Test())
        {
            diff.Add("settings");
        }

        return diff;
    }

    /// <summary>
    /// Gets the resolved input settings based on the action specified in the input.
    /// </summary>
    /// <returns>>A Json object representing the resolved input settings.</returns>
    public JObject GetResolvedInput()
    {
        Debug.Assert(this.Input.Settings != null, "Input settings should not be null.");
        if (this.resolvedInputUserSettings == null)
        {
            if (SettingsResourceObject.ActionFull.Equals(this.Input.Action, StringComparison.OrdinalIgnoreCase))
            {
                this.Output.Action = SettingsResourceObject.ActionFull;
                this.resolvedInputUserSettings = this.GetValidSettings(this.Input.Settings);
            }
            else
            {
                this.Output.Action = SettingsResourceObject.ActionPartial;
                var mergedSettings = this.GetUserSettings();
                mergedSettings.Merge(this.Input.Settings);
                this.resolvedInputUserSettings = this.GetValidSettings(mergedSettings);
            }
        }

        return this.resolvedInputUserSettings;
    }

    /// <summary>
    /// Retrieves a deep-cloned JSON representation of the current user settings.
    /// </summary>
    /// <returns>A Json object representing the user settings.</returns>
    public JObject GetUserSettings()
    {
        this.userSettings ??= UserSettings.ToJson();
        return (JObject)this.userSettings.DeepClone();
    }

    /// <summary>
    /// Writes the current output settings to persistent storage.
    /// </summary>
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
