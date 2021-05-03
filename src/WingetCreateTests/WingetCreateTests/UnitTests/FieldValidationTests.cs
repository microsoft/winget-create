// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using NUnit.Framework;
    using Sharprompt;

    /// <summary>
    /// Unit tests for verifying that validation takes place for the field inputs.
    /// </summary>
    public class FieldValidationTests
    {
        private const string ValidInputFailedValidationString = "Valid input failed validation.";

        private const string InvalidInputPassedValidationString = "Invalid input passed validation";

        private const string FieldRequiredString = "The {0} field is required.";

        private const string InputDoesNotMatchRegularExpressionString = "Input does not match the valid format pattern for this field.";

        private const string FailedToDisplayErrorMessageString = "Failed to display correct error message.";

        private const string FieldMustBeStringWithMinimumLengthString = "The field {0} must be a string with a minimum length";

        private readonly SingletonManifest manifest = new SingletonManifest();

#pragma warning disable IDE0042 // Deconstruct variable declaration
        /// <summary>
        /// Verifies the validation for the Name field.
        /// </summary>
        [Test]
        public void ValidateName()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "PackageName");
            var validInputResponse = TryValidator("Microsoft Powertoys", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator(null, property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            StringAssert.Contains(string.Format(FieldRequiredString, "PackageName"), invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }

        /// <summary>
        /// Verifies the validation for the Publisher field.
        /// </summary>
        [Test]
        public void ValidatePublisher()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "Publisher");
            var validInputResponse = TryValidator("Microsoft Corporation", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator(null, property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            StringAssert.Contains(string.Format(FieldRequiredString, "Publisher"), invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }

        /// <summary>
        /// Verifies the validation for the Id field.
        /// </summary>
        [Test]
        public void ValidatePackageIdentifier()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "PackageIdentifier");
            var validInputResponse = TryValidator("Microsoft.PowerToys", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator("MicrosoftPowerToys", property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            Assert.AreEqual(InputDoesNotMatchRegularExpressionString, invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }

        /// <summary>
        /// Verifies the validation for the Version field.
        /// </summary>
        [Test]
        public void ValidateVersion()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "PackageVersion");
            var validInputResponse = TryValidator("10.0.0", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator("/\\*:?", property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            Assert.AreEqual(InputDoesNotMatchRegularExpressionString, invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }

        /// <summary>
        /// Verifies the validation for the License field.
        /// </summary>
        [Test]
        public void ValidateLicense()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "License");
            var validInputResponse = TryValidator("License for Microsoft Powertoys", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator("a", property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            StringAssert.Contains(string.Format(FieldMustBeStringWithMinimumLengthString, "License"), invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }

        /// <summary>
        /// Verifies the validation for the License field.
        /// </summary>
        [Test]
        public void ValidateDescription()
        {
            var property = FieldValidation.ValidateProperty(this.manifest, "ShortDescription");
            var validInputResponse = TryValidator("Description for Microsoft Powertoys", property);
            Assert.That(validInputResponse.Result, Is.True, ValidInputFailedValidationString);

            var invalidInputResponse = TryValidator(null, property);
            Assert.That(invalidInputResponse.Result, Is.False, InvalidInputPassedValidationString);
            StringAssert.Contains(string.Format(FieldRequiredString, "ShortDescription"), invalidInputResponse.Message, FailedToDisplayErrorMessageString);
        }
#pragma warning restore IDE0042 // Deconstruct variable declaration

        /// <summary>
        /// Helper method to test whether the error validation is successful.
        /// </summary>
        /// <param name="input">Input to be validated.</param>
        /// <param name="validator">Validator function.</param>
        /// <returns>True if validation passed.</returns>
        private static (bool Result, string Message) TryValidator(object input, Func<object, ValidationResult> validator)
        {
            if (validator == null)
            {
                return (true, string.Empty);
            }

            var result = validator(input);

            if (result != null)
            {
                return (false, result.ErrorMessage);
            }
            else
            {
                return (true, string.Empty);
            }
        }
    }
}
