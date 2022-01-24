// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

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
                Type type = instance.GetType().GetProperty(memberName).PropertyType;
                List<ValidationResult> validationResults = new List<ValidationResult>();

                if (typeof(IEnumerable<string>).IsAssignableFrom(type) || (defaultValue != null && typeof(IEnumerable<string>).IsAssignableFrom(defaultValue.GetType())))
                {
                    // If the user didn't provide a value, check if null is allowed for field
                    var items = property == null
                        ? new List<string> { null }
                        : (property as string).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                    // If the original type of the field is a string, validate each item in the enumerable against the property
                    if (type == typeof(string))
                    {
                        foreach (var item in items)
                        {
                            if (!Validator.TryValidateProperty(property, validationContext, validationResults))
                            {
                                string result = JoinErrorMessages(validationResults);
                                return new ValidationResult(result);
                            }
                        }

                        return null;
                    }

                    // If the original type of the field is not a string, validate as a List<string>
                    property = items;
                }
                else if (type == typeof(long))
                {
                    property = long.Parse((string)property);
                }

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
