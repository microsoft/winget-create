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
                    new SubmitCommand { Path = "<PathToManifest>", GitHubToken = "<GitHubPersonalAccessToken>", PRTitle = "<PullRequestTitle>" });
            }
        }

        /// <summary>
        /// Gets or sets the path to a singleton manifest file.
        /// </summary>
        [Value(0, MetaName = "Path", Required = true, HelpText = "Path_HelpText", ResourceType = typeof(Resources))]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the previous version to replace from the Windows Package Manager repository.
        /// </summary>
        [Value(1, MetaName = "ReplaceVersion", Required = false, HelpText = "ReplaceVersion_HelpText", ResourceType = typeof(Resources))]
        public string ReplaceVersion { get; set; }

        /// <summary>
        /// Gets or sets the title for the pull request.
        /// </summary>
        [Option('p', "prtitle", Required = false, HelpText = "PullRequestTitle_HelpText", ResourceType = typeof(Resources))]
        public override string PRTitle { get => base.PRTitle; set => base.PRTitle = value; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to replace a previous version of the manifest with the update.
        /// </summary>
        [Option('r', "replace", Required = false, HelpText = "ReplacePrevious_HelpText", ResourceType = typeof(Resources))]
        public bool Replace { get; set; }

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

        /// <summary>
        /// Gets or sets a value indicating whether the PR should be opened automatically in the browser.
        /// </summary>
        [Option('n', "no-open", Required = false, HelpText = "NoOpenPRInBrowser_HelpText", ResourceType = typeof(Resources))]
        public bool NoOpenPRInBrowser { get => !this.OpenPRInBrowser; set => this.OpenPRInBrowser = !value; }

        /// <summary>
        /// Gets or sets the unbound arguments that exist after the first positional parameter.
        /// </summary>
        [Value(1, Hidden = true)]
        public IList<string> UnboundArgs { get; set; } = new List<string>();

        /// <summary>
        /// Executes the submit command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(SubmitCommand),
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                if (this.UnboundArgs.Any())
                {
                    Logger.ErrorLocalized(nameof(Resources.UnboundArguments_Message), string.Join(" ", this.UnboundArgs));
                    Logger.WarnLocalized(nameof(Resources.VerifyCommandUsage_Message));
                    return false;
                }

                if (!await this.LoadGitHubClient(true))
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
            string expandedPath = System.Environment.ExpandEnvironmentVariables(this.Path);

            // TODO: Remove singleton support.
            if (File.Exists(expandedPath) && ValidateManifest(expandedPath, this.Format))
            {
                Manifests manifests = new Manifests();
                manifests.SingletonManifest = Serialization.DeserializeFromPath<SingletonManifest>(expandedPath);

                if (this.Replace && !await this.ValidateReplaceArguments(manifests.SingletonManifest.PackageIdentifier, manifests.SingletonManifest.PackageVersion))
                {
                    return false;
                }

                return await this.GitHubSubmitManifests(manifests, this.PRTitle, this.Replace, this.ReplaceVersion);
            }
            else if (Directory.Exists(expandedPath) && ValidateManifest(expandedPath, this.Format))
            {
                List<string> manifestContents = Directory.GetFiles(expandedPath).Select(f => File.ReadAllText(f)).ToList();
                Manifests manifests = Serialization.DeserializeManifestContents(manifestContents);

                if (this.Replace && !await this.ValidateReplaceArguments(manifests.VersionManifest.PackageIdentifier, manifests.VersionManifest.PackageVersion))
                {
                    return false;
                }

                return await this.GitHubSubmitManifests(manifests, this.PRTitle, this.Replace, this.ReplaceVersion);
            }
            else
            {
                Logger.ErrorLocalized(nameof(Resources.Error_Prefix), Resources.PathDoesNotExist_Warning);
                return false;
            }
        }

        private async Task<bool> ValidateReplaceArguments(string packageId, string submitVersion)
        {
            string exactId;
            try
            {
                exactId = await this.GitHubClient.FindPackageId(packageId);
            }
            catch (Octokit.RateLimitExceededException)
            {
                Logger.ErrorLocalized(nameof(Resources.RateLimitExceeded_Message));
                return false;
            }

            if (string.IsNullOrEmpty(exactId))
            {
                Logger.ErrorLocalized(nameof(Resources.ReplacePackageIdDoesNotExist_Error), packageId);
                return false;
            }

            if (!string.IsNullOrEmpty(this.ReplaceVersion))
            {
                // If submit version is same as replace version, it's a regular update.
                if (submitVersion == this.ReplaceVersion)
                {
                    Logger.ErrorLocalized(nameof(Resources.ReplaceVersionEqualsSubmitVersion_ErrorMessage));
                    return false;
                }

                // Check if the replace version exists in the repository.
                try
                {
                    await this.GitHubClient.GetManifestContentAsync(packageId, this.ReplaceVersion);
                }
                catch (Octokit.NotFoundException)
                {
                    Logger.ErrorLocalized(nameof(Resources.VersionDoesNotExist_Error), this.ReplaceVersion, packageId);
                    return false;
                }
            }

            return true;
        }
    }
}
