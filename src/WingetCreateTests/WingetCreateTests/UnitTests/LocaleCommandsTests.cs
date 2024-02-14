// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

    /// <summary>
    /// Test cases for commands that deal with creating/updating locales.
    /// </summary>
    public class LocaleCommandsTests
    {
        /// <summary>
        /// Checks whether locale validations are working as expected.
        /// </summary>
        [Test]
        public void VerifyLocaleValidations()
        {
            var initialManifestContent = TestUtils.GetInitialMultifileManifestContent("Multifile.MsixTest");
            Manifests manifests = Serialization.DeserializeManifestContents(initialManifestContent);

            // Verify with different casing.
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("en-us", manifests), manifests.DefaultLocaleManifest, "The locale manifest for en-US should be returned.");
            ClassicAssert.IsTrue(LocaleHelper.DoesLocaleManifestExist("en-us", manifests), "Locale for en-US already exists in the manifest.");
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("en-USA", manifests), manifests.DefaultLocaleManifest, "The locale manifest for en-US should be returned.");
            ClassicAssert.IsTrue(LocaleHelper.DoesLocaleManifestExist("en-USA", manifests), "Locale for en-US already exists in the manifest.");
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("en-GB", manifests), manifests.LocaleManifests[0], "The locale manifest for en-GB should be returned.");
            ClassicAssert.IsTrue(LocaleHelper.DoesLocaleManifestExist("en-GB", manifests), "Locale for en-GB already exists in the manifest.");
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("en-gb", manifests), manifests.LocaleManifests[0], "Locale for en-GB already exists in the manifest.");
            ClassicAssert.IsTrue(LocaleHelper.DoesLocaleManifestExist("en-gb", manifests), "Locale for en-GB already exists in the manifest.");

            // Check for non-existent locales.
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("fr-FR", manifests), null, "Null should be returned as the locale manifest for fr-FR does not exist.");
            ClassicAssert.IsFalse(LocaleHelper.DoesLocaleManifestExist("fr-FR", manifests), "Locale for fr-FR does not exist in the manifest.");
            ClassicAssert.AreEqual(LocaleHelper.GetMatchingLocaleManifest("de-DE", manifests), null, "Null should be returned as the locale manifest for de-DE does not exist.");
            ClassicAssert.IsFalse(LocaleHelper.DoesLocaleManifestExist("de-DE", manifests), "Locale for de-DE does not exist in the manifest.");

            // Check for invalid locale formats.
            Assert.Throws<ArgumentException>(() => LocaleHelper.GetMatchingLocaleManifest("en", manifests), "Locale is not in the correct format.");
            Assert.Throws<ArgumentException>(() => LocaleHelper.DoesLocaleManifestExist("en", manifests), "Locale is not in the correct format.");
            Assert.Throws<ArgumentException>(() => LocaleHelper.GetMatchingLocaleManifest("fr_FR", manifests), "Locale is not in the correct format.");
            Assert.Throws<ArgumentException>(() => LocaleHelper.DoesLocaleManifestExist("fr_FR", manifests), "Locale is not in the correct format.");
        }

        /// <summary>
        /// Tests the ability to convert a DefaultLocaleManifest to a LocaleManifest.
        /// </summary>
        [Test]
        public void VerifyDefaultLocaleToLocaleManifestTypeConversion()
        {
            var manifestContent = TestUtils.GetInitialManifestContent("TestPublisher.LocaleConversionTest.yaml");
            Manifests manifest = Serialization.DeserializeManifestContents(manifestContent);

            DefaultLocaleManifest defaultLocale = manifest.DefaultLocaleManifest;
            LocaleManifest localeManifest = defaultLocale.ToLocaleManifest();

            ClassicAssert.IsTrue(localeManifest.ManifestType == "locale", "The manifest type should be locale.");

            ClassicAssert.AreEqual(defaultLocale.PackageIdentifier, localeManifest.PackageIdentifier, "The package identifier should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PackageVersion, localeManifest.PackageVersion, "The package version should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PackageLocale, localeManifest.PackageLocale, "The package locale should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Publisher, localeManifest.Publisher, "The publisher should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PublisherUrl, localeManifest.PublisherUrl, "The publisher URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PublisherSupportUrl, localeManifest.PublisherSupportUrl, "The publisher support URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PrivacyUrl, localeManifest.PrivacyUrl, "The privacy URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Author, localeManifest.Author, "The author should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PackageName, localeManifest.PackageName, "The package name should be the same.");
            ClassicAssert.AreEqual(defaultLocale.PackageUrl, localeManifest.PackageUrl, "The package URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.License, localeManifest.License, "The license should be the same.");
            ClassicAssert.AreEqual(defaultLocale.LicenseUrl, localeManifest.LicenseUrl, "The license URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Copyright, localeManifest.Copyright, "The copyright should be the same.");
            ClassicAssert.AreEqual(defaultLocale.CopyrightUrl, localeManifest.CopyrightUrl, "The copyright URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.ShortDescription, localeManifest.ShortDescription, "The short description should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Description, localeManifest.Description, "The description should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Tags, localeManifest.Tags, "The tags should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[0].IconUrl, localeManifest.Icons[0].IconUrl, "First icon URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[0].IconResolution, localeManifest.Icons[0].IconResolution, "First icon resolution should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[0].IconSha256, localeManifest.Icons[0].IconSha256, "First icon SHA256 should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[0].IconFileType.ToString(), localeManifest.Icons[0].IconFileType.ToString(), "First icon file type should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[0].IconTheme, localeManifest.Icons[0].IconTheme, "First icon theme should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[1].IconUrl, localeManifest.Icons[1].IconUrl, "Second icon URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[1].IconResolution, localeManifest.Icons[1].IconResolution, "Second icon resolution should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[1].IconSha256, localeManifest.Icons[1].IconSha256, "Second icon SHA256 should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[1].IconFileType.ToString(), localeManifest.Icons[1].IconFileType.ToString(), "Second icon file type should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Icons[1].IconTheme, localeManifest.Icons[1].IconTheme, "Second icon theme should be the same.");
            ClassicAssert.AreEqual(defaultLocale.ReleaseNotes, localeManifest.ReleaseNotes, "The release notes should be the same.");
            ClassicAssert.AreEqual(defaultLocale.ReleaseNotesUrl, localeManifest.ReleaseNotesUrl, "The release notes URL should be the same.");
            ClassicAssert.AreEqual(defaultLocale.InstallationNotes, localeManifest.InstallationNotes, "The installation notes should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Documentations[0].DocumentUrl, localeManifest.Documentations[0].DocumentUrl, "First document url should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Documentations[0].DocumentLabel, localeManifest.Documentations[0].DocumentLabel, "First document label should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Documentations[1].DocumentUrl, localeManifest.Documentations[1].DocumentUrl, "Second document url should be the same.");
            ClassicAssert.AreEqual(defaultLocale.Documentations[1].DocumentLabel, localeManifest.Documentations[1].DocumentLabel, "Second document url should be the same.");
            ClassicAssert.AreEqual(defaultLocale.ManifestVersion, localeManifest.ManifestVersion, "The manifest version should be the same.");

            // Skipped due to bad conversion model from schema.
            // ClassicAssert.AreEqual(defaultLocale.Agreements, localeManifest.Agreements, "The agreements should be the same.");
        }
    }
}
