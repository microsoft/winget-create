// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.WingetCreateCLI;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

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
            // Replace with the directory separator character for the current platform (e.g. '/' on Unix, '\' on Windows).
            string examplePath = "\\foo\\bar\\baz".Replace("\\", Path.DirectorySeparatorChar.ToString());
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string path1 = userProfilePath + examplePath;
            string path2 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + examplePath;
            string path3 = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) + examplePath;

            string substitutedPath1 = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%USERPROFILE%" : "~") + examplePath;
            string substitutedPath2 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%LOCALAPPDATA%" + examplePath : path2.Replace(userProfilePath, "~");
            string substitutedPath3 = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%TEMP%" + examplePath : path3;

            ClassicAssert.AreEqual(substitutedPath1, Common.GetPathForDisplay(path1, true), "The path does not contain the expected substitutions.");
            ClassicAssert.AreEqual(path1, Common.GetPathForDisplay(path1, false), "The path should not contain any substitutions.");

            ClassicAssert.AreEqual(substitutedPath2, Common.GetPathForDisplay(path2, true), "The path does not contain the expected substitutions.");
            ClassicAssert.AreEqual(path2, Common.GetPathForDisplay(path2, false), "The path should not contain any substitutions.");

            ClassicAssert.AreEqual(substitutedPath3, Common.GetPathForDisplay(path3, true), "The path does not contain the expected substitutions.");
            ClassicAssert.AreEqual(path3, Common.GetPathForDisplay(path3, false), "The path should not contain any substitutions.");
        }
    }
}
