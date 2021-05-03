// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Provides functionality for validating properties of the manifest object model.
    /// </summary>
    public static class FieldValidation
    {
        /// <summary>
        /// Validates a specified property.
        /// </summary>
        /// <param name="instance">Object instance. </param>
        /// <param name="memberName">Name of the property field. </param>
        /// <param name="defaultValue">Default property value. </param>
        /// <returns>Sharprompt Validation Result object. </returns>
        public static Func<object, ValidationResult> ValidateProperty(object instance, string memberName, object defaultValue = null)
        {
            return property =>
            {
                property = property is string propString && string.IsNullOrEmpty(propString) ? defaultValue : property;

                if (property != null)
                {
                    property = property.ToString().Trim();
                }

                var validationContext = new ValidationContext(instance) { MemberName = memberName };
                var validationResults = new List<ValidationResult>();
                if (Validator.TryValidateProperty(property, validationContext, validationResults))
                {
                    return null;
                }
                else
                {
                    string result = JoinErrorMessages(validationResults);
                    return new ValidationResult(result);
                }
            };
        }

        private static string JoinErrorMessages(List<ValidationResult> results)
        {
            List<string> message = new List<string>();

            foreach (ValidationResult result in results)
            {
                // Swap out regex errors with more user-friendly error message.
                if (result.ErrorMessage.Contains("regular expression"))
                {
                    message.Add(Properties.Resources.RegexFieldValidation_Error);
                }
                else
                {
                    message.Add(result.ErrorMessage);
                }
            }

            return string.Join(Environment.NewLine, message);
        }
    }
}
