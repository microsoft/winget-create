// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models
{
    using Newtonsoft.Json;

    /// <summary>
    /// A representation of an OWC publisher app.
    /// </summary>
    public record PublisherApp(string Publisher, string App, string Id);
}
