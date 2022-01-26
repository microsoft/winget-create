// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.Singleton
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using YamlDotNet.Serialization;

    /// <summary>
    /// Partial class that extends the property definitions of Agreement.
    /// </summary>
    public partial class Agreement
    {
        /// <summary>Gets or sets the agreement text content.</summary>
        [YamlMember(Alias = "Agreement")]
        [System.ComponentModel.DataAnnotations.StringLength(10000, MinimumLength = 1)]
        public string AgreementContent { get; set; }
    }

    /// <summary>
    /// Partial Installer class for defining a string type ReleaseDateTime field.
    /// Workaround for issue with model generating ReleaseDate with DateTimeOffset type.
    /// </summary>
    public partial class Installer
    {
        /// <summary>
        /// Gets or sets the Release Date time.
        /// </summary>
        [YamlMember(Alias = "ReleaseDate")]
        [RegularExpression(@"^(0[1-9]|1[012])[- /.](0[1-9]|[12][0-9]|3[01])[- /.](19|20)\d\d$")]
        public string ReleaseDateTime { get; set; }
    }

    /// <summary>
    /// This partial class is a workaround to a known issue.
    /// NSwag does not handle the "oneof" keyword of the Markets field, resulting in missing model classes.
    /// Because of this, the Markets and Markets array classes do not get generated, therefore we need to define them here
    /// under the default NSwag generated name "Markets2".
    /// </summary>
    public partial class Markets2
    {
        /// <summary>
        /// Gets or sets the list of allowed installer target markets.
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public List<string> AllowedMarkets { get; set; }

        /// <summary>
        /// Gets or sets the list of excluded installer target markets.
        /// </summary>
        [System.ComponentModel.DataAnnotations.MaxLength(256)]
        public List<string> ExcludedMarkets { get; set; }
    }
}
