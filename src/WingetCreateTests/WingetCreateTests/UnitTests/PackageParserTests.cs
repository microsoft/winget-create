// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System.IO;
    using System.Linq;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Unit tests to verify downloading installers and parsing the package file.
    /// </summary>
    public class PackageParserTests
    {
        /// <summary>
        /// One-time function that runs at the beginning of this test class.
        /// </summary>
        [OneTimeSetUp]
        public void Setup()
        {
            TestUtils.InitializeMockDownload();
        }

        /// <summary>
        /// One-time function that runs at the end of this test class.
        /// </summary>
        [OneTimeTearDown]
        public void Cleanup()
        {
            File.Delete(Path.Combine(Path.GetTempPath(), TestConstants.TestExeInstaller));
            File.Delete(Path.Combine(Path.GetTempPath(), TestConstants.TestMsiInstaller));
            File.Delete(Path.Combine(Path.GetTempPath(), TestConstants.TestMsixInstaller));
            PackageParser.SetHttpMessageHandler(null);
        }

        /// <summary>
        /// Downloads the EXE installer file from HTTPS localhost and parses the package to create a manifest object.
        /// </summary>
        [Test]
        public void ParseExeInstallerFile()
        {
            var testExeInstallerPath = MockDownloadFile(TestConstants.TestExeInstaller);
            Assert.That(testExeInstallerPath, Is.Not.Null.And.Not.Empty);

            Manifests manifests = new Manifests();

            Assert.IsTrue(PackageParser.ParsePackage(testExeInstallerPath, TestConstants.TestExeInstaller, manifests));
            Assert.AreEqual("WingetCreateTestExeInstaller", manifests.DefaultLocaleManifest.PackageName);
            Assert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            Assert.AreEqual("MicrosoftCorporation.WingetCreateTestExeInstaller", manifests.VersionManifest.PackageIdentifier);
            Assert.AreEqual("Microsoft Copyright", manifests.DefaultLocaleManifest.License);
            Assert.AreEqual(InstallerType.Exe, manifests.InstallerManifest.Installers.First().InstallerType);
        }

        /// <summary>
        /// Downloads the MSI installer file from HTTPS localhost and parses the package to create a manifest object.
        /// </summary>
        [Test]
        public void ParseMsiInstallerFile()
        {
            var testMsiInstallerPath = MockDownloadFile(TestConstants.TestMsiInstaller);
            Assert.That(testMsiInstallerPath, Is.Not.Null.And.Not.Empty);

            Manifests manifests = new Manifests();

            Assert.IsTrue(PackageParser.ParsePackage(testMsiInstallerPath, TestConstants.TestExeInstaller, manifests));
            Assert.AreEqual("WingetCreateTestMsiInstaller", manifests.DefaultLocaleManifest.PackageName);
            Assert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            Assert.AreEqual("MicrosoftCorporation.WingetCreateTestMsiInstaller", manifests.VersionManifest.PackageIdentifier);
            Assert.AreEqual(InstallerType.Msi, manifests.InstallerManifest.Installers.First().InstallerType);
            Assert.AreEqual("{E2650EFC-DCD3-4FAA-BBAC-FD1812B03A61}", manifests.InstallerManifest.Installers.First().ProductCode);
        }

        /// <summary>
        /// Downloads the MSIX installer file from HTTPS localhost and parses the package to create a manifest object.
        /// </summary>
        [Test]
        public void ParseMsixInstallerFile()
        {
            var testMsixInstallerPath = MockDownloadFile(TestConstants.TestMsixInstaller);
            Assert.That(testMsixInstallerPath, Is.Not.Null.And.Not.Empty);

            Manifests manifests = new Manifests();

            Assert.IsTrue(PackageParser.ParsePackage(testMsixInstallerPath, TestConstants.TestMsixInstaller, manifests));
            Assert.AreEqual("WingetCreateTestMsixInstaller", manifests.DefaultLocaleManifest.PackageName);
            Assert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            Assert.AreEqual("1.0.1.0", manifests.VersionManifest.PackageVersion);
            Assert.AreEqual("MicrosoftCorporation.WingetCreateTestMsixInstaller", manifests.VersionManifest.PackageIdentifier);
            Assert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.First().InstallerType);
            Assert.AreEqual(2, manifests.InstallerManifest.Installers.Count);
        }

        /// <summary>
        /// Validates that multiple installer URLs works.
        /// </summary>
        [Test]
        public void ParseMultipleInstallers()
        {
            var testMsiInstallerPath = MockDownloadFile(TestConstants.TestMsiInstaller);
            Assert.That(testMsiInstallerPath, Is.Not.Null.And.Not.Empty);
            var testExeInstallerPath = MockDownloadFile(TestConstants.TestExeInstaller);
            Assert.That(testExeInstallerPath, Is.Not.Null.And.Not.Empty);
            var testMsixInstallerPath = MockDownloadFile(TestConstants.TestMsixInstaller);
            Assert.That(testMsixInstallerPath, Is.Not.Null.And.Not.Empty);

            Manifests manifests = new Manifests();

            Assert.IsTrue(PackageParser.ParsePackages(
                new[] { testExeInstallerPath, testMsiInstallerPath, testMsixInstallerPath },
                new[] { TestConstants.TestExeInstaller, TestConstants.TestMsiInstaller, TestConstants.TestMsixInstaller },
                manifests));

            // Shared properties will be parsed from all installers, with priority given to the first-parsed value.
            Assert.AreEqual("WingetCreateTestExeInstaller", manifests.DefaultLocaleManifest.PackageName);
            Assert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            Assert.AreEqual("1.2.3.4", manifests.VersionManifest.PackageVersion);
            Assert.AreEqual("MicrosoftCorporation.WingetCreateTestExeInstaller", manifests.VersionManifest.PackageIdentifier);

            Assert.AreEqual(4, manifests.InstallerManifest.Installers.Count);
            Assert.AreEqual(InstallerType.Exe, manifests.InstallerManifest.Installers.First().InstallerType);
            Assert.AreEqual(InstallerType.Msi, manifests.InstallerManifest.Installers.Skip(1).First().InstallerType);
            Assert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.Skip(2).First().InstallerType);
            Assert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.Skip(3).First().InstallerType);
        }

        private static string MockDownloadFile(string filename)
        {
            string url = $"https://fakedomain.com/{filename}";
            TestUtils.SetMockHttpResponseContent(filename);
            string downloadedPath = PackageParser.DownloadFileAsync(url).Result;
            return downloadedPath;
        }
    }
}
