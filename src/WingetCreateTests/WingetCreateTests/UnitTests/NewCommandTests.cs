// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using NUnit.Framework;

    /// <summary>
    /// Test cases for verifying that the "new" command is working as expected.
    /// </summary>
    public class NewCommandTests
    {
        /// <summary>
        /// Verifies that the CLI errors out on an invalid installer URL.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task InvalidUrl()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);

            Logger.Initialize();
            NewCommand command = new NewCommand { InstallerUrls = new[] { "invalidUrl" } };
            Assert.IsFalse(await command.Execute(), "Command should have failed");
            string actual = sw.ToString();
            Assert.That(actual, Does.Contain(Resources.DownloadFile_Error), "Failed to catch invalid URL");
        }
    }
}
