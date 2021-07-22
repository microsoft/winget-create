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
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateTests;
    using NUnit.Framework;

    /// <summary>
    /// Unit test class for the Settings Command.
    /// </summary>
    public class CacheCommandTests
    {
        /// <summary>
        /// OneTimeSetup method for the cache command unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Logger.Initialize();
            TestUtils.InitializeMockDownload();
            PackageParser.InstallerDownloadPath = Path.Combine(Path.GetTempPath(), "wingetcreatetests");
        }

        /// <summary>
        /// OneTimeTearDown method for the cache command unit tests.
        /// </summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            PackageParser.InstallerDownloadPath = PackageParser.DefaultInstallerDownloadPath;
        }

        /// <summary>
        /// Setup method for the cache command unit tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            TestUtils.MockDownloadFile(TestConstants.TestMsiInstaller);
        }

        /// <summary>
        /// Verifies that if the List flag is present, shows the downloaded installer present in the cache folder.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task ListCachedInstallers()
        {
            StringWriter sw = new StringWriter();
            Console.SetOut(sw);
            CacheCommand command = new CacheCommand() { List = true };
            await command.Execute();
            string result = sw.ToString();
            int installerCount = Directory.GetFiles(PackageParser.InstallerDownloadPath).Length;
            Assert.That(result, Does.Contain(string.Format(Resources.InstallersFound_Message, installerCount, PackageParser.InstallerDownloadPath)));
            Assert.That(result, Does.Contain(TestConstants.TestMsiInstaller));
        }

        /// <summary>
        /// Verifies that if the Clean flag is present, deletes all downloaded installers present in the cache folder.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task CleanCachedInstallers()
        {
            CacheCommand command = new CacheCommand() { Clean = true };
            await command.Execute();
            var installerFiles = Directory.GetFiles(PackageParser.InstallerDownloadPath);
            Assert.AreEqual(0, installerFiles.Length, "Cached installers were not deleted.");
        }
    }
}
