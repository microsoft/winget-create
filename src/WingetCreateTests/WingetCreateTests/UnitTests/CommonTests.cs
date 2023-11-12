// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying common functions for the CLI.
    /// </summary>
    public class CommonTests
    {
        /// <summary>
        /// Tests the ability to retrieve the path for display purposes.
        /// </summary>
        [Test]
        public void VerifyPathSubstitutions()
        {
            string examplePath = "\\foo\\bar\\baz";
            string path1 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + examplePath;
            string path2 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + examplePath;
            string path3 = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + examplePath;

            string substitutedPath1 = "%USERPROFILE%" + examplePath;
            string substitutedPath2 = "%LOCALAPPDATA%" + examplePath;
            string substitutedPath3 = "%TEMP%" + examplePath;

            Assert.AreEqual(substitutedPath1, Common.GetPathForDisplay(path1, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path1, Common.GetPathForDisplay(path1, false), "The path should not contain any substitutions.");

            Assert.AreEqual(substitutedPath2, Common.GetPathForDisplay(path2, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path2, Common.GetPathForDisplay(path2, false), "The path should not contain any substitutions.");

            Assert.AreEqual(substitutedPath3, Common.GetPathForDisplay(path3, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path3, Common.GetPathForDisplay(path3, false), "The path should not contain any substitutions.");
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

            Assert.IsTrue(localeManifest.ManifestType == "locale", "The manifest type should be locale.");

            Assert.AreEqual(defaultLocale.PackageIdentifier, localeManifest.PackageIdentifier, "The package identifier should be the same.");
            Assert.AreEqual(defaultLocale.PackageVersion, localeManifest.PackageVersion, "The package version should be the same.");
            Assert.AreEqual(defaultLocale.PackageLocale, localeManifest.PackageLocale, "The package locale should be the same.");
            Assert.AreEqual(defaultLocale.Publisher, localeManifest.Publisher, "The publisher should be the same.");
            Assert.AreEqual(defaultLocale.PublisherUrl, localeManifest.PublisherUrl, "The publisher URL should be the same.");
            Assert.AreEqual(defaultLocale.PublisherSupportUrl, localeManifest.PublisherSupportUrl, "The publisher support URL should be the same.");
            Assert.AreEqual(defaultLocale.PrivacyUrl, localeManifest.PrivacyUrl, "The privacy URL should be the same.");
            Assert.AreEqual(defaultLocale.Author, localeManifest.Author, "The author should be the same.");
            Assert.AreEqual(defaultLocale.PackageName, localeManifest.PackageName, "The package name should be the same.");
            Assert.AreEqual(defaultLocale.PackageUrl, localeManifest.PackageUrl, "The package URL should be the same.");
            Assert.AreEqual(defaultLocale.License, localeManifest.License, "The license should be the same.");
            Assert.AreEqual(defaultLocale.LicenseUrl, localeManifest.LicenseUrl, "The license URL should be the same.");
            Assert.AreEqual(defaultLocale.Copyright, localeManifest.Copyright, "The copyright should be the same.");
            Assert.AreEqual(defaultLocale.CopyrightUrl, localeManifest.CopyrightUrl, "The copyright URL should be the same.");
            Assert.AreEqual(defaultLocale.ShortDescription, localeManifest.ShortDescription, "The short description should be the same.");
            Assert.AreEqual(defaultLocale.Description, localeManifest.Description, "The description should be the same.");
            Assert.AreEqual(defaultLocale.Tags, localeManifest.Tags, "The tags should be the same.");
            Assert.AreEqual(defaultLocale.Icons[0].IconUrl, localeManifest.Icons[0].IconUrl, "First icon URL should be the same.");
            Assert.AreEqual(defaultLocale.Icons[0].IconResolution, localeManifest.Icons[0].IconResolution, "First icon resolution should be the same.");
            Assert.AreEqual(defaultLocale.Icons[0].IconSha256, localeManifest.Icons[0].IconSha256, "First icon SHA256 should be the same.");
            Assert.AreEqual(defaultLocale.Icons[0].IconFileType.ToString(), localeManifest.Icons[0].IconFileType.ToString(), "First icon file type should be the same.");
            Assert.AreEqual(defaultLocale.Icons[0].IconTheme, localeManifest.Icons[0].IconTheme, "First icon theme should be the same.");
            Assert.AreEqual(defaultLocale.Icons[1].IconUrl, localeManifest.Icons[1].IconUrl, "Second icon URL should be the same.");
            Assert.AreEqual(defaultLocale.Icons[1].IconResolution, localeManifest.Icons[1].IconResolution, "Second icon resolution should be the same.");
            Assert.AreEqual(defaultLocale.Icons[1].IconSha256, localeManifest.Icons[1].IconSha256, "Second icon SHA256 should be the same.");
            Assert.AreEqual(defaultLocale.Icons[1].IconFileType.ToString(), localeManifest.Icons[1].IconFileType.ToString(), "Second icon file type should be the same.");
            Assert.AreEqual(defaultLocale.Icons[1].IconTheme, localeManifest.Icons[1].IconTheme, "Second icon theme should be the same.");
            Assert.AreEqual(defaultLocale.ReleaseNotes, localeManifest.ReleaseNotes, "The release notes should be the same.");
            Assert.AreEqual(defaultLocale.ReleaseNotesUrl, localeManifest.ReleaseNotesUrl, "The release notes URL should be the same.");
            Assert.AreEqual(defaultLocale.InstallationNotes, localeManifest.InstallationNotes, "The installation notes should be the same.");
            Assert.AreEqual(defaultLocale.Documentations[0].DocumentUrl, localeManifest.Documentations[0].DocumentUrl, "First document url should be the same.");
            Assert.AreEqual(defaultLocale.Documentations[0].DocumentLabel, localeManifest.Documentations[0].DocumentLabel, "First document label should be the same.");
            Assert.AreEqual(defaultLocale.Documentations[1].DocumentUrl, localeManifest.Documentations[1].DocumentUrl, "Second document url should be the same.");
            Assert.AreEqual(defaultLocale.Documentations[1].DocumentLabel, localeManifest.Documentations[1].DocumentLabel, "Second document url should be the same.");
            Assert.AreEqual(defaultLocale.ManifestVersion, localeManifest.ManifestVersion, "The manifest version should be the same.");

            // Skipped due to bad conversion model from schema.
            // Assert.AreEqual(defaultLocale.Agreements, localeManifest.Agreements, "The agreements should be the same.");
        }
    }
}
