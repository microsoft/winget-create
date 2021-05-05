// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using Newtonsoft.Json;

    /// <summary>
    /// A representation of an OWC publisher app.
    /// </summary>
    public record PublisherApp(string Publisher, string App, string Id);

    /// <summary>
    /// A representation of an OWC publisher app with a defined version.
    /// </summary>
    public record PublisherAppVersion(string Publisher, string App, string Id, string Version, [property: JsonIgnore] string Path)
        : PublisherApp(Publisher, App, Id);

    /// <summary>
    /// A representation of an OWC publisher app with a defined content and version.
    /// </summary>
    public record PublisherAppVersionContent(string Publisher, string App, string Id, string Version, string Path, string Content)
        : PublisherAppVersion(Publisher, App, Id, Version, Path);
}
