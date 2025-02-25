// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests
{
    using System;
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
        private const string TokenEnvironmentVariable = "WINGET_CREATE_GITHUB_TOKEN";

        /// <summary>
        /// OneTimeSetup method for the unit tests.
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Logger.Initialize();
        }

        /// <summary>
        /// SetUp method for the unit tests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            TokenHelper.Delete();
            Environment.SetEnvironmentVariable(TokenEnvironmentVariable, null);
        }

        /// <summary>
        /// TearDown method for the unit tests.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            TokenHelper.Delete();
            Environment.SetEnvironmentVariable(TokenEnvironmentVariable, null);
        }

        /// <summary>
        /// Test case for verifying that the "token" can be read from the environment variable after clearing the token cache.
        /// </summary>
        [Test]
        public async Task TokenClearAndReadFromEnvironmentVariable()
        {
            await this.ExecuteTokenClearCommand();
            ClassicAssert.IsFalse(TokenHelper.TryRead(out var _), "Token cache shouldn't exist");

            Environment.SetEnvironmentVariable(TokenEnvironmentVariable, "MockToken");
            ClassicAssert.IsTrue(TokenHelper.TryRead(out var _), "Token cache should exist after setting environment variable");
        }

        /// <summary>
        /// Test case for verifying that the "token --clear" command is working as expected.
        /// </summary>
        [Test]
        public async Task TokenClearCommand()
        {
            await this.ExecuteTokenStoreCommand();
            await this.ExecuteTokenClearCommand();
            ClassicAssert.IsFalse(TokenHelper.TryRead(out var _), "Token cache shouldn't exist after running Token --clear command");
        }

        /// <summary>
        /// Test case for verifying that the "token --store" command is working as expected.
        /// </summary>
        [Test]
        public async Task TokenStoreCommand()
        {
            await this.ExecuteTokenClearCommand();
            await this.ExecuteTokenStoreCommand();
            ClassicAssert.IsTrue(TokenHelper.TryRead(out var _), "Token cache should exist after running Token --store command");
        }

        /// <summary>
        /// Executes the token clear command.
        /// </summary>
        private async Task ExecuteTokenClearCommand()
        {
            var command = new TokenCommand { Clear = true };
            ClassicAssert.IsTrue(await command.Execute(), "Command should have succeeded");
        }

        /// <summary>
        /// Executes the token store command.
        /// </summary>
        private async Task ExecuteTokenStoreCommand()
        {
            var command = new TokenCommand { Store = true, GitHubToken = this.GitHubApiKey };
            ClassicAssert.IsTrue(await command.Execute(), "Command should have succeeded");
        }
    }
}
