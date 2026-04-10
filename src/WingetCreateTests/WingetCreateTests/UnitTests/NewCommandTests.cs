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
    using NUnit.Framework.Legacy;

    /// <summary>
    /// Test cases for verifying that the "new" command is working as expected.
    /// </summary>
    public class NewCommandTests
    {
        /// <summary>
        /// OneTimeSetup method for the New command unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Logger.Initialize();
        }

        /// <summary>
        /// Verifies that the CLI errors out on an invalid installer URL.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task InvalidUrl()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);

            NewCommand command = new NewCommand { InstallerUrls = new[] { "invalidUrl" } };
            ClassicAssert.IsFalse(await command.Execute(), "Command should have failed");
            string actual = sw.ToString();
            Assert.That(actual, Does.Contain(Resources.DownloadFile_Error), "Failed to catch invalid URL");
        }

        /// <summary>
        /// Verifies that the CLI errors out on an invalid protocol.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task InvalidProtocol()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);

            NewCommand command = new NewCommand { InstallerUrls = new[] { "ftp://mock" }, AllowUnsecureDownloads = true };
            ClassicAssert.IsFalse(await command.Execute(), "Command should have failed");
            string actual = sw.ToString();
            Assert.That(actual, Does.Contain(Resources.DownloadProtocolNotSupported_Error));
            Assert.That(command.AllowUnsecureDownloads, Is.True);
        }

        /// <summary>
        /// Tests that the command execution fails when using non-HTTPS URLs.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task HttpsOnly()
        {
            using StringWriter sw = new StringWriter();
            Console.SetOut(sw);

            NewCommand command = new NewCommand { InstallerUrls = new[] { "http://mock" } };
            ClassicAssert.IsFalse(await command.Execute(), "Command should have failed");
            string actual = sw.ToString();
            Assert.That(actual, Does.Contain(Resources.DownloadHttpsOnly_Error));
            Assert.That(command.AllowUnsecureDownloads, Is.False);
        }
    }
}
