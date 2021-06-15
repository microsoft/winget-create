// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Singleton;
    using Microsoft.WingetCreateCore.Models.Version;

    /// <summary>
    /// Command for updating the elements of an existing or local manifest.
    /// </summary>
    [Verb("update", HelpText = "UpdateCommand_HelpText", ResourceType = typeof(Resources))]
    public class UpdateCommand : BaseCommand
    {
        /// <summary>
        /// Gets or sets the package files used for parsing and extracting relevant installer metadata.
        /// </summary>
        private readonly List<string> packageFiles = new();

        /// <summary>
        /// Gets the usage examples for the update command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_UpdateCommand_SearchAndUpdateVersion, new UpdateCommand { Id = "<PackageIdentifier>", Version = "<Version>" });
                yield return new Example(Resources.Example_UpdateCommand_SearchAndUpdateInstallerURL, new UpdateCommand { Id = "<PackageIdentifier>", InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>" } });
                yield return new Example(Resources.Example_UpdateCommand_SaveAndPublish, new UpdateCommand { Id = "<PackageIdentifier>", Version = "<Version>", OutputDir = "<OutputDirectory>", GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the new value used to update the manifest version element.
        /// </summary>
        [Option('v', "version", Required = false, HelpText = "Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the outputPath where the generated manifest file should be saved to.
        /// </summary>
        [Option('o', "out", Required = false, HelpText = "OutputDirectory_HelpText", ResourceType = typeof(Resources))]
        public string OutputDir { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the updated manifest should be submitted to Github.
        /// </summary>
        [Option('s', "submit", Required = false, HelpText = "SubmitToWinget_HelpText", ResourceType = typeof(Resources))]
        public bool SubmitToGitHub { get; set; }

        /// <summary>
        /// Gets or sets the new value(s) used to update the manifest installer elements.
        /// </summary>
        [Option('u', "urls", Required = false, HelpText = "InstallerUrl_HelpText", ResourceType = typeof(Resources))]
        public IEnumerable<string> InstallerUrls { get; set; }

        /// <summary>
        /// Executes the update command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(UpdateCommand),
                InstallerUrls = this.InstallerUrls,
                Id = this.Id,
                Version = this.Version,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                Logger.DebugLocalized(nameof(Resources.RetrievingManifest_Message), this.Id);

                GitHub client = new GitHub(null, this.WingetRepoOwner, this.WingetRepo);
                string exactId = await client.FindPackageId(this.Id);

                if (!string.IsNullOrEmpty(exactId))
                {
                    this.Id = exactId;
                }

                List<string> latestManifestContent;

                try
                {
                    latestManifestContent = await client.GetLatestManifestContentAsync(this.Id);
                }
                catch (Octokit.NotFoundException e)
                {
                    Logger.ErrorLocalized(nameof(Resources.Error_Prefix), e.Message);
                    Logger.ErrorLocalized(nameof(Resources.OctokitNotFound_Error));
                    return false;
                }

                return await this.ExecuteManifestUpdate(latestManifestContent, commandEvent);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        /// <summary>
        /// Convert list of yaml contents to Manifests object model, and then update them.
        /// </summary>
        /// <param name="latestManifestContent">List of manifests to be updated.</param>
        /// <returns>Manifests object representing the updates manifest content, or null if the update failed.</returns>
        public async Task<Manifests> DeserializeExistingManifestsAndUpdate(List<string> latestManifestContent)
        {
            Manifests manifests = new Manifests();

            Serialization.DeserializeManifestContents(latestManifestContent, manifests);

            if (manifests.SingletonManifest != null)
            {
                manifests = ConvertSingletonToMultifileManifest(manifests.SingletonManifest);
            }

            VersionManifest versionManifest = manifests.VersionManifest;
            InstallerManifest installerManifest = manifests.InstallerManifest;
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;
            List<LocaleManifest> localeManifests = manifests.LocaleManifests;

            // Ensure that capitalization matches between folder structure and package ID
            versionManifest.PackageIdentifier = this.Id;
            installerManifest.PackageIdentifier = this.Id;
            defaultLocaleManifest.PackageIdentifier = this.Id;
            UpdatePropertyForLocaleManifests(nameof(LocaleManifest.PackageIdentifier), this.Id, localeManifests);

            if (!string.IsNullOrEmpty(this.Version))
            {
                versionManifest.PackageVersion = this.Version;
                installerManifest.PackageVersion = this.Version;
                defaultLocaleManifest.PackageVersion = this.Version;
                UpdatePropertyForLocaleManifests(nameof(LocaleManifest.PackageVersion), this.Version, localeManifests);
            }

            if (!this.InstallerUrls.Any())
            {
                this.InstallerUrls = installerManifest.Installers.Select(i => i.InstallerUrl).ToArray();
            }

            bool previousManifestHasMultipleUrls = installerManifest.Installers.Select(i => i.InstallerUrl).Distinct().Count() > 1;
            bool updatingManifestWithMultipleUrls = this.InstallerUrls.Distinct().Count() > 1;

            foreach (var url in this.InstallerUrls)
            {
                string packageFile = await DownloadPackageFile(url);
                if (string.IsNullOrEmpty(packageFile))
                {
                    return null;
                }

                // Updating an msixbundle is only supported when specifying a single distinct URL
                if (updatingManifestWithMultipleUrls && PackageParser.IsPackageMsixBundle(packageFile))
                {
                    Logger.Error(Resources.MultipleInstallerUrlFound_Error);
                    return null;
                }

                this.packageFiles.Add(packageFile);
            }

            PackageParser.UpdateInstallerNodes(installerManifest, this.InstallerUrls, this.packageFiles);

            return manifests;
        }

        /// <summary>
        /// Executes the manifest update flow.
        /// </summary>
        /// <param name="latestManifestContent">List of manifests to be updated.</param>
        /// <param name="commandEvent">CommandExecuted telemetry event.</param>
        /// <returns>Boolean representing whether the manifest was updated successfully or not.</returns>
        public async Task<bool> ExecuteManifestUpdate(List<string> latestManifestContent, CommandExecutedEvent commandEvent)
        {
            Manifests manifests = await this.DeserializeExistingManifestsAndUpdate(latestManifestContent);
            if (manifests == null)
            {
                return false;
            }

            DisplayManifestPreview(manifests);

            if (string.IsNullOrEmpty(this.OutputDir))
            {
                this.OutputDir = Directory.GetCurrentDirectory();
            }

            string manifestDirectoryPath = SaveManifestDirToLocalPath(manifests, this.OutputDir);

            if (ValidateManifest(manifestDirectoryPath))
            {
                if (this.SubmitToGitHub)
                {
                    return await this.SetAndCheckGitHubToken()
                        ? (commandEvent.IsSuccessful = await this.GitHubSubmitManifests(manifests, this.GitHubToken))
                        : false;
                }

                return commandEvent.IsSuccessful = true;
            }
            else
            {
                return false;
            }
        }

        private static Manifests ConvertSingletonToMultifileManifest(SingletonManifest singletonManifest)
        {
            // Create automapping configuration
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<SingletonManifest, VersionManifest>().ForMember(dest => dest.DefaultLocale, opt => { opt.MapFrom(src => src.PackageLocale); });
                cfg.CreateMap<SingletonManifest, DefaultLocaleManifest>();
                cfg.CreateMap<SingletonManifest, InstallerManifest>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Dependencies, WingetCreateCore.Models.Installer.Dependencies>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Installer, WingetCreateCore.Models.Installer.Installer>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.InstallerSwitches, WingetCreateCore.Models.Installer.InstallerSwitches>();
            });
            var mapper = config.CreateMapper();

            Manifests manifests = new Manifests();

            manifests.VersionManifest = mapper.Map<VersionManifest>(singletonManifest);
            manifests.InstallerManifest = mapper.Map<InstallerManifest>(singletonManifest);
            manifests.DefaultLocaleManifest = mapper.Map<DefaultLocaleManifest>(singletonManifest);

            // ManifestType gets overwritten by source. Revert to proper ManifestType.
            manifests.VersionManifest.ManifestType = "version";
            manifests.InstallerManifest.ManifestType = "installer";
            manifests.DefaultLocaleManifest.ManifestType = "defaultLocale";

            return manifests;
        }

        private static void UpdatePropertyForLocaleManifests(string propertyName, string value, List<LocaleManifest> localeManifests)
        {
            foreach (LocaleManifest manifest in localeManifests)
            {
                manifest.GetType().GetProperty(propertyName).SetValue(manifest, value);
            }
        }
    }
}
