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
        /// Valid PackageIdentifier to be used in test cases.
        /// </summary>
        public const string TestInvalidPackageIdentifier = "testpublisher.testapp";

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
    }
}
