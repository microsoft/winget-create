// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

using Microsoft.WingetCreateCLI.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Represents a settings resource object used in DSC operations.
/// </summary>
public class SettingsResourceObject : BaseResourceObject
{
    /// <summary>
    /// Defines the action type for full settings application.
    /// </summary>
    public const string ActionFull = "Full";

    /// <summary>
    /// Defines the action type for partial settings application.
    /// </summary>
    public const string ActionPartial = "Partial";

    /// <summary>
    /// Gets or sets the settings for the resource object.
    /// </summary>
    [JsonProperty("settings", Required = Required.Always)]
    public JObject Settings { get; set; }

    /// <summary>
    /// Gets or sets the action to be performed on the settings resource object.
    /// </summary>
    [JsonProperty("action", NullValueHandling = NullValueHandling.Ignore)]
    public string Action { get; set; }

    /// <inheritdoc/>
    public override JObject GetProperties()
    {
        var baseProperties = base.GetProperties();
        baseProperties["settings"] = new JObject
        {
            ["description"] = Resources.DscResourcePropertyDescriptionSettings,
            ["type"] = "object",
        };
        baseProperties["action"] = new JObject
        {
            ["default"] = ActionPartial,
            ["description"] = Resources.DscResourcePropertyDescriptionAction,
            ["type"] = "string",
            ["enum"] = new JArray(ActionFull, ActionPartial),
        };
        return baseProperties; ;
    }

    /// <inheritdoc/>
    public override JArray GetRequiredProperties()
    {
        return ["settings"];
    }
}
