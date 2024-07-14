// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateTests
{
    using Microsoft.WingetCreateCLI.Models.Settings;

    /// <summary>
    /// Shared string constants to be used in test cases.
    /// </summary>
    public static class TestConstants
    {
        /// <summary>
        /// Valid PackageIdentifier to be used in test cases.
        /// </summary>
        public const string TestPackageIdentifier = "TestPublisher.TestApp";

        /// <summary>
        /// Test pull request title to be used in test cases.
        /// </summary>
        public const string TestPRTitle = "TestPublisher.TestApp.TestTitle";

        /// <summary>
        /// Invalid PackageIdentifier to be used in test cases.
        /// </summary>
        public const string TestInvalidPackageIdentifier = "testpublisher.testapp";

        /// <summary>
        /// PackageIdentifier with multiple installers to be used in test cases.
        /// </summary>
        public const string TestMultipleInstallerPackageIdentifier = "TestPublisher.MultipleInstallerApp";

        /// <summary>
        /// File name of the test EXE installer.
        /// </summary>
        public const string TestExeInstaller = "WingetCreateTestExeInstaller.exe";

        /// <summary>
        /// File name of the test portable installer.
        /// </summary>
        public const string TestPortableInstaller = "WingetCreateTestPortableInstaller.exe";

        /// <summary>
        /// File name of the test MSI installer.
        /// </summary>
        public const string TestMsiInstaller = "WingetCreateTestMsiInstaller.msi";

        /// <summary>
        /// File name of the test MSIX installer.
        /// </summary>
        public const string TestMsixInstaller = "WingetCreateTestMsixInstaller.msixbundle";

        /// <summary>
        /// File name of the test ZIP installer.
        /// </summary>
        public const string TestZipInstaller = "WingetCreateTestZipInstaller.zip";

        /// <summary>
        /// Format argument to be used for YAML submissions.
        /// </summary>
        public const ManifestFormat YamlManifestFormat = ManifestFormat.Yaml;

        /// <summary>
        /// Format argument to be used for JSON submissions.
        /// </summary>
        public const ManifestFormat JsonManifestFormat = ManifestFormat.Json;

        /// <summary>
        /// Test constants to use for YAML manifest tests.
        /// </summary>
        public static class YamlConstants
        {
            /// <summary>
            /// PackageIdentifier for test exe.
            /// </summary>
            public const string TestExePackageIdentifier = "WingetCreateE2E.Yaml.ExeTest";

            /// <summary>
            /// PackageIdentifier for test msi.
            /// </summary>
            public const string TestMsiPackageIdentifier = "WingetCreateE2E.Yaml.MsiTest";

            /// <summary>
            /// File name of the test msix manifest.
            /// </summary>
            public const string TestMultifileMsixPackageIdentifier = "Multifile.Yaml.MsixTest";

            /// <summary>
            /// PackageIdentifier for test portable.
            /// </summary>
            public const string TestPortablePackageIdentifier = "WingetCreateE2E.Yaml.PortableTest";

            /// <summary>
            /// PackageIdentifier for test zip.
            /// </summary>
            public const string TestZipPackageIdentifier = "WingetCreateE2E.Yaml.ZipTest";

            /// <summary>
            /// File name of the test exe manifest.
            /// </summary>
            public const string TestExeManifest = "WingetCreateE2E.Yaml.ExeTest.yaml";

            /// <summary>
            /// File name of the test msi manifest.
            /// </summary>
            public const string TestMsiManifest = "WingetCreateE2E.Yaml.MsiTest.yaml";

            /// <summary>
            /// File name of the test portable manifest.
            /// </summary>
            public const string TestPortableManifest = "WingetCreateE2E.Yaml.PortableTest.yaml";

            /// <summary>
            /// Path of the directory with the multifile msix test manifests.
            /// </summary>
            public const string TestMultifileMsixManifestDir = "Multifile.Yaml.MsixTest";

            /// <summary>
            ///  File name of the test zip manifest.
            /// </summary>
            public const string TestZipManifest = "WingetCreateE2E.Yaml.ZipTest.yaml";
        }

        /// <summary>
        /// Test constants to use for JSON manifest tests.
        /// </summary>
        public static class JsonConstants
        {
            /// <summary>
            /// PackageIdentifier for test exe.
            /// </summary>
            public const string TestExePackageIdentifier = "WingetCreateE2E.Json.ExeTest";

            /// <summary>
            /// PackageIdentifier for test msi.
            /// </summary>
            public const string TestMsiPackageIdentifier = "WingetCreateE2E.Json.MsiTest";

            /// <summary>
            /// File name of the test msix manifest.
            /// </summary>
            public const string TestMultifileMsixPackageIdentifier = "Multifile.Json.MsixTest";

            /// <summary>
            /// PackageIdentifier for test portable.
            /// </summary>
            public const string TestPortablePackageIdentifier = "WingetCreateE2E.Json.PortableTest";

            /// <summary>
            /// PackageIdentifier for test zip.
            /// </summary>
            public const string TestZipPackageIdentifier = "WingetCreateE2E.Json.ZipTest";

            /// <summary>
            /// File name of the test exe manifest.
            /// </summary>
            public const string TestExeManifest = "WingetCreateE2E.Json.ExeTest.json";

            /// <summary>
            /// File name of the test msi manifest.
            /// </summary>
            public const string TestMsiManifest = "WingetCreateE2E.Json.MsiTest.json";

            /// <summary>
            /// File name of the test portable manifest.
            /// </summary>
            public const string TestPortableManifest = "WingetCreateE2E.Json.PortableTest.json";

            /// <summary>
            /// Path of the directory with the multifile msix test manifests.
            /// </summary>
            public const string TestMultifileMsixManifestDir = "Multifile.Json.MsixTest";

            /// <summary>
            ///  File name of the test zip manifest.
            /// </summary>
            public const string TestZipManifest = "WingetCreateE2E.Json.ZipTest.json";
        }
    }
}
