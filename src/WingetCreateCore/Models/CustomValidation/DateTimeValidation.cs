// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCore.Models.CustomValidation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Custom validator for the date time strings.
    /// </summary>
    public class DateTimeValidation : ValidationAttribute
    {
        /// <inheritdoc/>
        public override bool IsValid(object value)
        {
            return DateTime.TryParse((string)value, out DateTime dateTime);
        }
    }
}
