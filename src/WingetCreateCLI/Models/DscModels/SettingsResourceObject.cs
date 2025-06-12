// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

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

    /// <summary>
    /// Creates a settings resource object with the specified settings and action.
    /// </summary>
    /// <param name="json">The JSON representation of the settings resource object.</param>
    /// <returns>A settings resource object.</returns>
    public static SettingsResourceObject FromJson(JToken json)
    {
        return json.ToObject<SettingsResourceObject>();
    }

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
