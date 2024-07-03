// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using AutoMapper;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

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
            var testExeInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestExeInstaller);
            Assert.That(testExeInstallerPath, Is.Not.Null.And.Not.Empty);
            Manifests manifests = new Manifests();
            var installerMetadataList = new List<InstallerMetadata>
            {
                new InstallerMetadata { InstallerUrl = TestConstants.TestExeInstaller, PackageFile = testExeInstallerPath },
            };

            Assert.DoesNotThrow(() => PackageParser.ParsePackages(installerMetadataList, manifests));
            ClassicAssert.AreEqual("WingetCreateTestExeInstaller", manifests.DefaultLocaleManifest.PackageName);
            ClassicAssert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            ClassicAssert.AreEqual("MicrosoftCorporation.WingetCreateTestExeInstaller", manifests.VersionManifest.PackageIdentifier);
            ClassicAssert.AreEqual("Microsoft Copyright", manifests.DefaultLocaleManifest.Copyright);
            ClassicAssert.AreEqual(InstallerType.Exe, manifests.InstallerManifest.Installers.First().InstallerType);
        }

        /// <summary>
        /// Downloads the MSI installer file from HTTPS localhost and parses the package to create a manifest object.
        /// </summary>
        [Test]
        public void ParseMsiInstallerFile()
        {
            var testMsiInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestMsiInstaller);
            Assert.That(testMsiInstallerPath, Is.Not.Null.And.Not.Empty);
            Manifests manifests = new Manifests();
            var installerMetadataList = new List<InstallerMetadata>
            {
                new InstallerMetadata { InstallerUrl = TestConstants.TestExeInstaller, PackageFile = testMsiInstallerPath },
            };

            Assert.DoesNotThrow(() => PackageParser.ParsePackages(installerMetadataList, manifests));
            ClassicAssert.AreEqual("WingetCreateTestMsiInstaller", manifests.DefaultLocaleManifest.PackageName);
            ClassicAssert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            ClassicAssert.AreEqual("MicrosoftCorporation.WingetCreateTestMsiInstaller", manifests.VersionManifest.PackageIdentifier);
            ClassicAssert.AreEqual(InstallerType.Msi, manifests.InstallerManifest.Installers.First().InstallerType);
            ClassicAssert.AreEqual("{E2650EFC-DCD3-4FAA-BBAC-FD1812B03A61}", manifests.InstallerManifest.Installers.First().ProductCode);
        }

        /// <summary>
        /// Downloads the MSIX installer file from HTTPS localhost and parses the package to create a manifest object.
        /// </summary>
        [Test]
        public void ParseMsixInstallerFile()
        {
            var testMsixInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestMsixInstaller);
            Assert.That(testMsixInstallerPath, Is.Not.Null.And.Not.Empty);
            Manifests manifests = new Manifests();
            var installerMetadataList = new List<InstallerMetadata>
            {
                new InstallerMetadata { InstallerUrl = TestConstants.TestMsixInstaller, PackageFile = testMsixInstallerPath },
            };

            Assert.DoesNotThrow(() => PackageParser.ParsePackages(installerMetadataList, manifests));
            ClassicAssert.AreEqual("WingetCreateTestMsixInstaller", manifests.DefaultLocaleManifest.PackageName);
            ClassicAssert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            ClassicAssert.AreEqual("MicrosoftCorporation.WingetCreateTestMsixInstaller", manifests.VersionManifest.PackageIdentifier);
            ClassicAssert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.First().InstallerType);
            ClassicAssert.AreEqual(2, manifests.InstallerManifest.Installers.Count);
        }

        /// <summary>
        /// Validates that multiple installer URLs works.
        /// </summary>
        [Test]
        public void ParseMultipleInstallers()
        {
            var testMsiInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestMsiInstaller);
            Assert.That(testMsiInstallerPath, Is.Not.Null.And.Not.Empty);
            var testExeInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestExeInstaller);
            Assert.That(testExeInstallerPath, Is.Not.Null.And.Not.Empty);
            var testMsixInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestMsixInstaller);
            Assert.That(testMsixInstallerPath, Is.Not.Null.And.Not.Empty);
            Manifests manifests = new Manifests();

            var installerMetadataList = new List<InstallerMetadata>
            {
                new InstallerMetadata { InstallerUrl = TestConstants.TestExeInstaller, PackageFile = testExeInstallerPath },
                new InstallerMetadata { InstallerUrl = TestConstants.TestMsiInstaller, PackageFile = testMsiInstallerPath },
                new InstallerMetadata { InstallerUrl = TestConstants.TestMsixInstaller, PackageFile = testMsixInstallerPath },
            };

            Assert.DoesNotThrow(() => PackageParser.ParsePackages(installerMetadataList, manifests));

            // Shared properties will be parsed from all installers, with priority given to the first-parsed value.
            ClassicAssert.AreEqual("WingetCreateTestExeInstaller", manifests.DefaultLocaleManifest.PackageName);
            ClassicAssert.AreEqual("Microsoft Corporation", manifests.DefaultLocaleManifest.Publisher);
            ClassicAssert.AreEqual("1.2.3.4", manifests.VersionManifest.PackageVersion);
            ClassicAssert.AreEqual("MicrosoftCorporation.WingetCreateTestExeInstaller", manifests.VersionManifest.PackageIdentifier);

            ClassicAssert.AreEqual(4, manifests.InstallerManifest.Installers.Count);
            ClassicAssert.AreEqual(InstallerType.Exe, manifests.InstallerManifest.Installers.First().InstallerType);
            ClassicAssert.AreEqual(InstallerType.Msi, manifests.InstallerManifest.Installers.Skip(1).First().InstallerType);
            ClassicAssert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.Skip(2).First().InstallerType);
            ClassicAssert.AreEqual(InstallerType.Msix, manifests.InstallerManifest.Installers.Skip(3).First().InstallerType);
        }

        /// <summary>
        /// Validates that the ParsePackageAndUpdateInstallerNode function works as expected.
        /// </summary>
        [Test]
        public void ParseAndUpdateInstaller()
        {
            var testMsiInstallerPath = TestUtils.MockDownloadFile(TestConstants.TestMsiInstaller);
            Assert.That(testMsiInstallerPath, Is.Not.Null.And.Not.Empty);

            List<string> initialManifestContent = TestUtils.GetInitialManifestContent($"TestPublisher.SingleMsixInExistingBundle.yaml");
            Manifests initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            WingetCreateCore.Models.Singleton.Installer initialInstaller = initialManifests.SingletonManifest.Installers.First();
            Installer installer = ConvertSingletonInstaller(initialInstaller);

            bool result = PackageParser.ParsePackageAndUpdateInstallerNode(installer, testMsiInstallerPath, installer.InstallerUrl);
            ClassicAssert.IsTrue(result, "ParsePackageAndUpdateInstallerNode should return true.");
            ClassicAssert.AreEqual(InstallerType.Msi, installer.InstallerType, "InstallerType should be updated.");
            ClassicAssert.AreEqual(initialInstaller.Architecture.ToEnumAttributeValue(), installer.Architecture.ToEnumAttributeValue(), "Architecture should not change.");
            ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, installer.InstallerSha256, "InstallerSha256 should be updated.");
            ClassicAssert.AreEqual("{E2650EFC-DCD3-4FAA-BBAC-FD1812B03A61}", installer.ProductCode, "ProductCode should be updated");
        }

        /// <summary>
        /// Validates that the ParsePackageAndUpdateInstallerNode function works as expected for a zip installer.
        /// </summary>
        [Test]
        public void ParseAndUpdateZipInstaller()
        {
            var testZipInstaller = TestUtils.MockDownloadFile(TestConstants.TestZipInstaller);
            Assert.That(testZipInstaller, Is.Not.Null.And.Not.Empty);
            string extractDirectory = Path.Combine(PackageParser.InstallerDownloadPath, Path.GetFileNameWithoutExtension(testZipInstaller));

            try
            {
                ZipFile.ExtractToDirectory(testZipInstaller, extractDirectory, true);
            }
            catch (Exception e)
            {
                ClassicAssert.Fail($"Failed to extract the zip file: {e.Message}");
            }

            List<string> initialManifestContent = TestUtils.GetInitialManifestContent($"TestPublisher.ZipWithExe.yaml");
            Manifests initialManifests = Serialization.DeserializeManifestContents(initialManifestContent);
            WingetCreateCore.Models.Singleton.Installer initialInstaller = initialManifests.SingletonManifest.Installers.First();
            Installer installer = ConvertSingletonInstaller(initialInstaller);
            string nestedInstallerPath = Path.Combine(extractDirectory, installer.NestedInstallerFiles.First().RelativeFilePath);

            bool result = PackageParser.ParsePackageAndUpdateInstallerNode(installer, nestedInstallerPath, installer.InstallerUrl, testZipInstaller);
            ClassicAssert.IsTrue(result, "ParsePackageAndUpdateInstallerNode should return true.");
            ClassicAssert.AreEqual(InstallerType.Zip, installer.InstallerType, "InstallerType should not change");
            ClassicAssert.AreEqual(initialInstaller.Architecture.ToEnumAttributeValue(), installer.Architecture.ToEnumAttributeValue(), "Architecture should not change.");
            ClassicAssert.AreNotEqual(initialInstaller.InstallerSha256, installer.InstallerSha256, "InstallerSha256 should be updated");
            ClassicAssert.AreEqual(installer.InstallerSha256, PackageParser.GetFileHash(testZipInstaller), "InstallSha256 should match the hash of the zip file");
        }

        /// <summary>
        /// Converts the SingletonManifest Installer object model to the InstallerManifest Installer object model.
        /// </summary>
        /// <param name="installer">Singleton Manifest Installer object model.</param>
        /// <returns>Installer Manifest Installer object model.</returns>
        private static Installer ConvertSingletonInstaller(WingetCreateCore.Models.Singleton.Installer installer)
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Dependencies, WingetCreateCore.Models.Installer.Dependencies>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.NestedInstallerFile, WingetCreateCore.Models.Installer.NestedInstallerFile>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Installer, WingetCreateCore.Models.Installer.Installer>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.InstallerSwitches, WingetCreateCore.Models.Installer.InstallerSwitches>();
            });
            var mapper = config.CreateMapper();

            return mapper.Map<Installer>(installer);
        }
    }
}
