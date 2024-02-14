// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.WingetCreateCLI;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using NUnit.Framework;
    using NUnit.Framework.Legacy;

    /// <summary>
    /// Test cases for verifying that the "token" command is working as expected.
    /// </summary>
    public class TokenCommandTests : GitHubTestsBase
    {
        /// <summary>
        /// Verifies that the Token command works as expected.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        public async Task TokenCommand()
        {
            Logger.Initialize();

            // Preemptively clear existing token
            var command = new TokenCommand { Clear = true };
            ClassicAssert.IsTrue(await command.Execute(), "Command should have succeeded");

            string tokenCacheFile = Path.Combine(Common.LocalAppStatePath, "tokenCache.bin");
            FileAssert.DoesNotExist(tokenCacheFile, "Token cache file shouldn't exist before running Token --store command");
            command = new TokenCommand { Store = true, GitHubToken = this.GitHubApiKey };
            ClassicAssert.IsTrue(await command.Execute(), "Command should have succeeded");
            FileAssert.Exists(tokenCacheFile, "Token cache file should exist after storing token");

            command = new TokenCommand { Clear = true };
            ClassicAssert.IsTrue(await command.Execute(), "Command should have succeeded");
            FileAssert.DoesNotExist(tokenCacheFile, "Token cache file shouldn't exist after running Token --clear command");
        }
    }
}
