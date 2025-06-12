// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Models.DscModels;

using Newtonsoft.Json;

/// <summary>
/// Represents a base resource object with a property indicating whether the resource is in its desired state.
/// </summary>
public class BaseResourceObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the resource is in its desired state.
    /// </summary>
    [JsonProperty("_inDesiredState", NullValueHandling = NullValueHandling.Ignore)]
    public bool? InDesiredState { get; set; }
}
