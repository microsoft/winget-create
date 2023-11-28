// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides helper functions for dealing with prompting and validating locale properties.
    /// </summary>
    public static class LocaleHelper
    {
        /// <summary>
        /// Prompts user to enter values for the input locale or default locale manifest properties.
        /// </summary>
        /// <typeparam name="T">Type of the manifest. Expected to be either LocaleManifest or DefaultLocaleManifest.</typeparam>
        /// <param name="localeManifest">Object model of the locale/defaultLocale manifest.</param>
        /// <param name="properties">List of property names to be prompted for.</param>
        /// <param name="originalManifests">Optional parameter to be used when validating the user-inputted locale. Check whether the locale already exists in the original manifests.</param>
        public static void PromptAndSetLocaleProperties<T>(T localeManifest, List<string> properties, Manifests originalManifests = null)
        {
            foreach (string propertyName in properties)
            {
                PropertyInfo property = typeof(T).GetProperty(propertyName);
                PromptHelper.PromptPropertyAndSetValue(localeManifest, propertyName, property.GetValue(localeManifest));

                if (propertyName == nameof(LocaleManifest.PackageLocale) && originalManifests != null)
                {
                    while (!ValidateLocale(property.GetValue(localeManifest).ToString(), originalManifests))
                    {
                        PromptHelper.PromptPropertyAndSetValue(localeManifest, propertyName, property.GetValue(localeManifest));
                    }

                    continue;
                }

                Logger.Trace($"Property [{propertyName}] set to the value [{property.GetValue(localeManifest)}]");
            }
        }

        /// <summary>
        /// Checks whether the provided locale is valid. A locale is valid if it is in the correct format and does not already exist in the manifest.
        /// This function handles the exception gracefully to be used in a prompt.
        /// </summary>
        /// <param name="locale">The package locale string to check.</param>
        /// <param name="manifests">The base manifests to check against.</param>
        /// <returns>A boolean value indicating whether the locale is valid.</returns>
        public static bool ValidateLocale(string locale, Manifests manifests)
        {
            try
            {
                if (GetMatchingLocaleManifest(locale, manifests) != null)
                {
                    Logger.ErrorLocalized(nameof(Resources.LocaleAlreadyExists_ErrorMessage), locale);
                    Console.WriteLine();
                    return false;
                }

                return true;
            }
            catch (ArgumentException)
            {
                Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                Console.WriteLine();
                return false;
            }
        }

        /// <summary>
        /// Checks whether package locale already exists in the default locale manifest or one of the locale manifests and returns the matching manifest.
        /// This function throws an exception if the locale string is in an invalid format.
        /// </summary>
        /// <param name="locale">The package locale string to check.</param>
        /// <param name="originalManifests">The base manifests to check against.</param>
        /// <returns>An object representing the matching locale manifest.</returns>
        public static object GetMatchingLocaleManifest(string locale, Manifests originalManifests)
        {
            RegionInfo localeInfo = new RegionInfo(locale);

            if (localeInfo.Equals(new RegionInfo(originalManifests.DefaultLocaleManifest.PackageLocale)))
            {
                return originalManifests.DefaultLocaleManifest;
            }

            foreach (var localeManifest in originalManifests.LocaleManifests)
            {
                if (localeInfo.Equals(new RegionInfo(localeManifest.PackageLocale)))
                {
                    return localeManifest;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the list of locale properties that have not been prompted for.
        /// </summary>
        /// <typeparam name="T">Type of the manifest. Expected to be either LocaleManifest or DefaultLocaleManifest.</typeparam>
        /// <param name="manifest">Object model of the locale/defaultLocale manifest.</param>
        /// <param name="promptedProperties">Properties that have already been prompted for.</param>
        /// <returns>List of locale property names.</returns>
        public static List<string> GetUnPromptedLocalePropertyNames<T>(T manifest, List<string> promptedProperties)
        {
            return manifest.GetType().GetProperties().ToList().Where(p =>
                                p.GetCustomAttribute<RequiredAttribute>() == null &&
                                p.GetCustomAttribute<JsonPropertyAttribute>() != null &&
                                !promptedProperties.Any(d => d == p.Name)).Select(p => p.Name).ToList();
        }
    }
}
