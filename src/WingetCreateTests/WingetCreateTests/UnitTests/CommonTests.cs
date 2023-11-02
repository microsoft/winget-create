// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using Microsoft.WingetCreateCLI;
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
            string path1 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\foo\\bar\\baz";
            string path2 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\foo\\bar\\baz";
            string path3 = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + "\\foo\\bar\\baz";

            string expectedPath1 = "%USERPROFILE%\\foo\\bar\\baz";
            string expectedPath2 = "%LOCALAPPDATA%\\foo\\bar\\baz";
            string expectedPath3 = "%TEMP%\\foo\\bar\\baz";

            Assert.AreEqual(expectedPath1, Common.GetPathForDisplay(path1, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path1, Common.GetPathForDisplay(path1, false), "The path should not contain any substitutions.");

            Assert.AreEqual(expectedPath2, Common.GetPathForDisplay(path2, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path2, Common.GetPathForDisplay(path2, false), "The path should not contain any substitutions.");

            Assert.AreEqual(expectedPath3, Common.GetPathForDisplay(path3, true), "The path does not contain the expected substitutions.");
            Assert.AreEqual(path3, Common.GetPathForDisplay(path3, false), "The path should not contain any substitutions.");
        }
    }
}
