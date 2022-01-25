// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.DefaultLocale
{
    using YamlDotNet.Serialization;

    /// <summary>
    /// Partial class that implements cloning functionality to the PackageDependencies class.
    /// </summary>
    public partial class Agreement
    {
        /// <summary>Gets or sets the agreement text content.</summary>
        [YamlMember(Alias = "Agreement")]
        [Newtonsoft.Json.JsonProperty("Agreement", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(10000, MinimumLength = 1)]
        public string AgreementContent { get; set; }
    }
}
