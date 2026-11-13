// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using Newtonsoft.Json;

    /// <summary>
    /// A representation of an OWC publisher app with a defined version.
    /// </summary>
    public record PublisherAppVersion(string Publisher, string App, string Id, string Version, [property: JsonIgnore] string Path)
        : PublisherApp(Publisher, App, Id);
}
