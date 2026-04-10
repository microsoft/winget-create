// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

using Microsoft.WingetCreateCLI.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Represents a base resource object with a property indicating whether the resource is in its desired state.
/// </summary>
public abstract class BaseResourceObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the resource is in its desired state.
    /// </summary>
    [JsonProperty("_inDesiredState", NullValueHandling = NullValueHandling.Ignore)]
    public bool? InDesiredState { get; set; }

    /// <summary>
    /// Gets the properties of the resource object.
    /// </summary>
    /// <returns>A Json object containing the properties of the resource.</returns>
    public virtual JObject GetProperties()
    {
        return new JObject
        {
            ["_inDesiredState"] = new JObject
            {
                ["description"] = Resources.DscResourcePropertyDescriptionInDesiredState,
                ["type"] = "boolean",
            },
        };
    }

    /// <summary>
    /// Gets the required properties of the resource object.
    /// </summary>
    /// <returns>A Json array containing the required properties.</returns>
    public abstract JArray GetRequiredProperties();

    /// <summary>
    /// Converts the current object to a JSON representation.
    /// </summary>
    /// <returns>A Json object representing the current object.</returns>
    public JObject ToJson()
    {
        return JObject.FromObject(this);
    }
}
