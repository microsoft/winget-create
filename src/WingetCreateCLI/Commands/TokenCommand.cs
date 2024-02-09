﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;

    /// <summary>
    /// Command to either update or delete the cached GitHub Oauth token.
    /// </summary>
    [Verb("token", HelpText = "TokenCommand_HelpText", ResourceType = typeof(Resources))]
    public class TokenCommand : BaseCommand
    {
        /// <summary>
        /// Gets the usage examples for the token command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_TokenCommand_StoreNewToken, new TokenCommand { Store = true, GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_TokenCommand_ClearExistingToken, new TokenCommand { Clear = true });
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to clear the cached GitHub token.
        /// </summary>
        [Option('c', "clear", SetName = nameof(Clear), Required = true, HelpText = "Clear_HelpText", ResourceType = typeof(Resources))]
        public bool Clear { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to set the cached GitHub token.
        /// </summary>
        [Option('s', "store", Required = true, SetName = nameof(Store), HelpText = "Store_HelpText", ResourceType = typeof(Resources))]
        public bool Store { get; set; }

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

        /// <summary>
        /// Executes the token command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(TokenCommand),
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                if (this.Clear)
                {
                    Logger.InfoLocalized(nameof(Resources.ClearToken_Message));
                    GitHubOAuth.DeleteTokenCache();
                    return commandEvent.IsSuccessful = true;
                }
                else if (this.Store)
                {
                    Logger.InfoLocalized(nameof(Resources.SettingToken_Message));
                    return commandEvent.IsSuccessful = string.IsNullOrEmpty(this.GitHubToken) ?
                        await this.GetTokenFromOAuth() :
                        this.StoreTokenInCache();
                }

                return false;
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }
    }
}
