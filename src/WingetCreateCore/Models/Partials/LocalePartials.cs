// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.Locale
{
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using YamlDotNet.Serialization;

    /// <summary>
    /// Partial class that extends the property definitions of LocaleManifest.
    /// </summary>
    public partial class LocaleManifest
    {
        /// <summary>
        /// Explicit cast from DefaultLocaleManifest to a LocaleManifest.
        /// </summary>
        /// <param name="defaultLocaleManifest">Represents the default locale manifest.</param>
        public static explicit operator LocaleManifest(DefaultLocaleManifest defaultLocaleManifest)
        {
            return defaultLocaleManifest.ToLocaleManifest();
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the Icon class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Icon
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of Icon objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            Icon other = (Icon)obj;
            return this.IconUrl == other.IconUrl
                && this.IconResolution == other.IconResolution
                && this.IconSha256 == other.IconSha256
                && this.IconFileType == other.IconFileType
                && this.IconTheme == other.IconTheme;
        }
    }

    /// <summary>
    /// Partial class that implements helper methods for the Documentation class.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Documentation
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>
        /// Gives the criteria for determining whether two instances of Documentation objects are equal.
        /// </summary>
        /// <returns>A boolean value indicating whether the two objects are equal.</returns>
        /// <param name="obj" type="object">The object to compare with the current object.</param>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            Documentation other = (Documentation)obj;
            return this.DocumentUrl == other.DocumentUrl
                && this.DocumentLabel == other.DocumentLabel;
        }
    }

    /// <summary>
    /// Partial class that extends the property definitions of Agreement.
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single type
    public partial class Agreement
#pragma warning restore SA1402 // File may only contain a single type
    {
        /// <summary>Gets or sets the agreement text content.</summary>
        [YamlMember(Alias = "Agreement")]
        [Newtonsoft.Json.JsonProperty("Agreement", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.StringLength(10000, MinimumLength = 1)]
        public string AgreementContent { get; set; }
    }
}
