// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying that strings are correctly localized.
    /// </summary>
    public class LocalizationTests
    {
        /// <summary>
        /// Verifies that all localized strings exist for every property that exists
        /// in the Manifest object model.
        /// </summary>
        [Test]
        public void VerifyLocalizedStringsForProperties()
        {
            this.VerifyPropertiesRecursively(typeof(SingletonManifest));
            this.VerifyPropertiesRecursively(typeof(VersionManifest));
            this.VerifyPropertiesRecursively(typeof(InstallerManifest));
            this.VerifyPropertiesRecursively(typeof(LocaleManifest));
            this.VerifyPropertiesRecursively(typeof(DefaultLocaleManifest));
        }

        /// <summary>
        /// Verifies that at least one string is actually getting localized and available in different languages at runtime.
        /// </summary>
        [Test]
        public void VerifyLocalizedString()
        {
            string defaultResourceValue = Resources.AppDescription_HelpText;
            Console.WriteLine($"Default: {defaultResourceValue}");

            var localizedCulture = new CultureInfo("de-DE");
            string localizedResourceValue = Resources.ResourceManager.GetString(nameof(Resources.AppDescription_HelpText), localizedCulture);
            Console.WriteLine($"{localizedCulture.Name}: {localizedResourceValue}");

            Assert.That(defaultResourceValue, Is.Not.Null.And.Not.Empty.And.Not.EqualTo(localizedResourceValue));
        }

        /// <summary>
        /// Shows missing localization languages, and missing resources for localized languages. This test is informational only.
        /// </summary>
        [Test]
        public void ShowMissingLocalizedStrings()
        {
            // Expected language set comes from https://setup.intlservices.microsoft.com/Home/ConfigSettings?TeamID=25160
            string[] expectedLanguages = new[] { "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "ko-KR", "pt-BR", "ru-RU", "zh-CN", "zh-TW" };

            var expectedResourceKeys = Resources.ResourceManager.GetResourceSet(new CultureInfo("en-us"), true, true)
                .Cast<DictionaryEntry>()
                .Select(r => r.Key as string)
                .ToList();

            int languageCount = 0;
            foreach (var language in expectedLanguages)
            {
                var culture = new CultureInfo(language);
                var resourceSet = Resources.ResourceManager.GetResourceSet(culture, true, false);
                if (resourceSet != null)
                {
                    languageCount++;

                    int languageResourceCount = 0;
                    foreach (var expectedResourceKey in expectedResourceKeys)
                    {
                        if (resourceSet.GetString(expectedResourceKey) != null)
                        {
                            languageResourceCount++;
                        }
                        else
                        {
                            Assert.Warn($"Localized resource {expectedResourceKey} missing for culture {culture.Name}");
                        }
                    }

                    Console.WriteLine($"Found {languageResourceCount} localized resources for {culture.Name} out of {expectedResourceKeys.Count} expected");
                }
                else
                {
                    Assert.Warn($"Localized resource file missing for culture {culture.Name}");
                }
            }

            Console.WriteLine($"Found {languageCount} localization files out of {expectedLanguages.Length} expected");

            Assert.Pass();
        }

        /// <summary>
        /// Recursively iterates through each property of the object model
        /// including any collections of objects that may exists.
        /// </summary>
        /// <param name="type">Type of object model.</param>
        private void VerifyPropertiesRecursively(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                {
                    // Checks properties for objects inside a collection
                    Type collectionType = property.PropertyType.GetGenericArguments().First();
                    if (collectionType != typeof(string) && collectionType != typeof(int))
                    {
                        this.VerifyPropertiesRecursively(collectionType);
                    }
                }
                else if (property.PropertyType != typeof(string) && property.PropertyType.IsClass)
                {
                    // Checks the properties for class objects
                    this.VerifyPropertiesRecursively(property.PropertyType);
                }

                string name = property.Name;
                string resourceString = Resources.ResourceManager.GetString($"{name}_KeywordDescription");

                Assert.That(resourceString, Is.Not.Null.And.Not.Empty, $"{name} is missing localized string");
            }
        }
    }
}
