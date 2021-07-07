// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateTests
{
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
        /// File name of the test MSI installer.
        /// </summary>
        public const string TestMsiInstaller = "WingetCreateTestMsiInstaller.msi";

        /// <summary>
        /// File name of the test MSIX installer.
        /// </summary>
        public const string TestMsixInstaller = "WingetCreateTestMsixInstaller.msixbundle";

        /// <summary>
        /// PackageIdentifier for test exe.
        /// </summary>
        public const string TestExePackageIdentifier = "WingetCreateE2E.ExeTest";

        /// <summary>
        /// PackageIdentifier for test msi.
        /// </summary>
        public const string TestMsiPackageIdentifier = "WingetCreateE2E.MsiTest";

        /// <summary>
        /// File name of the test msix manifest.
        /// </summary>
        public const string TestMultifileMsixPackageIdentifier = "Multifile.MsixTest";

        /// <summary>
        /// File name of the test exe manifest.
        /// </summary>
        public const string TestExeManifest = "WingetCreateE2E.ExeTest.yaml";

        /// <summary>
        /// File name of the test msi manifest.
        /// </summary>
        public const string TestMsiManifest = "WingetCreateE2E.MsiTest.yaml";

        /// <summary>
        /// Path of the directory with the multifile msix test manifests.
        /// </summary>
        public const string TestMultifileMsixManifestDir = "Multifile.MsixTest";
    }
}
