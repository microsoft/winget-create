// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;

    /// <summary>
    /// Show command to display manifest from the packages repository.
    /// </summary>
    [Verb("show", HelpText = "ShowCommand_HelpText", ResourceType = typeof(Resources))]
    public class ShowCommand : BaseCommand
    {
        /// <summary>
        /// Gets the usage examples for the update command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_ShowCommand_DisplayLatestManifest, new ShowCommand { Id = "<PackageIdentifier>" });
                yield return new Example(Resources.Example_ShowCommand_DisplaySpecifiedVersion, new ShowCommand { Id = "<PackageIdentifier>", Version = "<Version>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_ShowCommand_ShowInstallerAndDefaultLocale, new ShowCommand { Id = "<PackageIdentifier>", ShowInstallerManifest = true, ShowDefaultLocaleManifest = true, GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version of the package from the Windows Package Manager repository to display the manifest for.
        /// </summary>
        [Option('v', "version", Required = false, Default = null, HelpText = "ShowCommand_Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the installer manifest.
        /// </summary>
        [Option("installer-manifest", Required = false, HelpText = "InstallerManifest_HelpText", ResourceType = typeof(Resources))]
        public bool ShowInstallerManifest { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the default locale manifest.
        /// </summary>
        [Option("defaultlocale-manifest", Required = false, HelpText = "DefaultLocaleManifest_HelpText", ResourceType = typeof(Resources))]
        public bool ShowDefaultLocaleManifest { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the locale manifests.
        /// </summary>
        [Option("locale-manifests", Required = false, HelpText = "LocaleManifests_HelpText", ResourceType = typeof(Resources))]
        public bool ShowLocaleManifests { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the version manifest.
        /// </summary>
        [Option("version-manifest", Required = false, HelpText = "VersionManifest_HelpText", ResourceType = typeof(Resources))]
        public bool ShowVersionManifest { get; set; }

        /// <summary>
        /// Gets or sets the GitHub token for authenticated access to GitHub API.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "ShowCommand_GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

        /// <summary>
        /// Executes the show command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Id = this.Id,
                Version = this.Version,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string exactId;
            try
            {
                exactId = await this.GitHubClient.FindPackageId(this.Id);
            }
            catch (Octokit.RateLimitExceededException)
            {
                Logger.ErrorLocalized(nameof(Resources.RateLimitExceeded_Message));
                return false;
            }

            if (!string.IsNullOrEmpty(exactId))
            {
                this.Id = exactId;
            }

            List<string> latestManifestContent;

            try
            {
                latestManifestContent = await this.GitHubClient.GetManifestContentAsync(this.Id, this.Version);
                Manifests originalManifests = Serialization.DeserializeManifestContents(latestManifestContent);
                this.ParseArgumentsAndShowManifest(originalManifests);
                return await Task.FromResult(commandEvent.IsSuccessful = true);
            }
            catch (Octokit.NotFoundException e)
            {
                Logger.ErrorLocalized(nameof(Resources.Error_Prefix), e.Message);
                Logger.ErrorLocalized(nameof(Resources.OctokitNotFound_Error));
                return false;
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private static void ShowAllManifests(Manifests manifests)
        {
            DisplayInstallerManifest(manifests.InstallerManifest);
            DisplayDefaultLocaleManifest(manifests.DefaultLocaleManifest);
            DisplayLocaleManifests(manifests.LocaleManifests);
            DisplayVersionManifest(manifests.VersionManifest);
        }

        private static void DisplayInstallerManifest(InstallerManifest installerManifest)
        {
            Logger.InfoLocalized(nameof(Resources.InstallerManifest_Message));
            Console.WriteLine(installerManifest.ToYaml(true));
        }

        private static void DisplayVersionManifest(VersionManifest versionManifest)
        {
            Logger.InfoLocalized(nameof(Resources.VersionManifest_Message));
            Console.WriteLine(versionManifest.ToYaml(true));
        }

        private static void DisplayDefaultLocaleManifest(DefaultLocaleManifest defaultLocaleManifest)
        {
            Logger.InfoLocalized(nameof(Resources.DefaultLocaleManifest_Message), defaultLocaleManifest.PackageLocale);
            Console.WriteLine(defaultLocaleManifest.ToYaml(true));
        }

        private static void DisplayLocaleManifests(List<LocaleManifest> localeManifests)
        {
            foreach (var localeManifest in localeManifests)
            {
                Logger.InfoLocalized(nameof(Resources.LocaleManifest_Message), localeManifest.PackageLocale);
                Console.WriteLine(localeManifest.ToYaml(true));
            }
        }

        private static void DisplaySingletonManifest(SingletonManifest singletonManifest)
        {
            Logger.InfoLocalized(nameof(Resources.SingletonManifest_Message));
            Console.WriteLine(singletonManifest.ToYaml(true));
        }

        private void ParseArgumentsAndShowManifest(Manifests manifests)
        {
            if (manifests.SingletonManifest != null)
            {
                DisplaySingletonManifest(manifests.SingletonManifest);
                return;
            }

            bool showAll = !this.ShowInstallerManifest && !this.ShowDefaultLocaleManifest && !this.ShowLocaleManifests && !this.ShowVersionManifest;
            if (this.ShowInstallerManifest)
            {
                DisplayInstallerManifest(manifests.InstallerManifest);
            }

            if (this.ShowDefaultLocaleManifest || this.ShowLocaleManifests)
            {
                DisplayDefaultLocaleManifest(manifests.DefaultLocaleManifest);
            }

            if (this.ShowLocaleManifests)
            {
                DisplayLocaleManifests(manifests.LocaleManifests);
            }

            if (this.ShowVersionManifest)
            {
                DisplayVersionManifest(manifests.VersionManifest);
            }

            if (showAll)
            {
                ShowAllManifests(manifests);
            }
        }
    }
}
