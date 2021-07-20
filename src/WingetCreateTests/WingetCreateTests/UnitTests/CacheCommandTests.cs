// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
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
        /// OneTimeSetup method for the settings command unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Logger.Initialize();
            TestUtils.InitializeMockDownload();
        }

        [SetUp]
        public void SetUp()
        {
            TestUtils.MockDownloadFile(TestConstants.TestMsiInstaller);
        }

        /// <summary>
        /// Verifies that if the List flag is present, shows the downloaded installer present in the cache folder.
        /// </summary>
        [Test]
        public void ListCachedInstallers()
        {
            StringWriter sw = new StringWriter();
            System.Console.SetOut(sw);
            CacheCommand command = new CacheCommand() { List = true };
            command.Execute();
            string result = sw.ToString();

            Assert.That(result, Does.Contain(string.Format(Resources.InstallersFound_Message, 1, PackageParser.InstallerDownloadPath)));
            Assert.That(result, Does.Contain(TestConstants.TestMsiInstaller));
        }

        /// <summary>
        /// Verifies that if the Clean flag is present, deletes all downloaded installers present in the cache folder.
        /// </summary>
        [Test]
        public void CleanCachedInstallers()
        {
            CacheCommand command = new CacheCommand() { Clean = true };
            command.Execute();
            var installerFiles = Directory.GetFiles(PackageParser.InstallerDownloadPath);
            Assert.IsTrue(installerFiles.Length == 0, "Cached installers were not deleted.");
        }
    }
}
