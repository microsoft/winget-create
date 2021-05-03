// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.Singleton;

    /// <summary>
    /// Command to validate and submit a manifest to the GitHub Windows Package Manager repository.
    /// </summary>
    [Verb("submit", HelpText = "SubmitCommand_HelpText", ResourceType = typeof(Resources))]
    public class SubmitCommand : BaseCommand
    {
        /// <summary>
        /// Gets the usage examples for the submit command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(
                    Resources.Example_SubmitCommand_SubmitLocalManifest,
                    new SubmitCommand { Path = "<PathToManifest>", GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the path to a singleton manifest file.
        /// </summary>
        [Value(0, MetaName = "Path", Required = true, HelpText = "Path_HelpText", ResourceType = typeof(Resources))]
        public string Path { get; set; }

        /// <summary>
        /// Executes the submit command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = "Submit",
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                if (!await this.SetAndCheckGitHubToken())
                {
                    return false;
                }

                return commandEvent.IsSuccessful = await this.SubmitManifest();
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private async Task<bool> SubmitManifest()
        {
            Manifests manifests = new Manifests();

            if (File.Exists(this.Path) && ValidateManifest(this.Path))
            {
                manifests.SingletonManifest = Serialization.DeserializeFromPath<SingletonManifest>(this.Path);
                return await this.GitHubSubmitManifests(manifests, this.GitHubToken);
            }
            else if (Directory.Exists(this.Path) && ValidateManifest(this.Path))
            {
                List<string> manifestContents = Directory.GetFiles(this.Path).Select(f => File.ReadAllText(f)).ToList();

                DeserializeManifestContents(manifestContents, manifests);
                return await this.GitHubSubmitManifests(manifests, this.GitHubToken);
            }
            else
            {
                Logger.ErrorLocalized(nameof(Resources.Error_Prefix), Resources.PathDoesNotExist_Warning);
                return false;
            }
        }
    }
}
