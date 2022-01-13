// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
namespace Microsoft.WingetCreateCore.Models
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

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

    // Move this to its own custom validation folder/class
    public class ValidateMarkets : ValidationAttribute
    {
        // Checks if the allowedmarket does not have any string values that overlap with the excludedmarkets and vice versa.
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var model = (Markets2)validationContext.ObjectInstance;
            return base.IsValid(value, validationContext);
        }
    }

    public class ValidateDateTimeOffset : ValidationAttribute
    {
        // Checks if the datetime string entered can successfully be parsed, if not return the error to the user

    }
}
