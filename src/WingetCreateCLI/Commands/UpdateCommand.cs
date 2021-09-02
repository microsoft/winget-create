// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
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
    using Microsoft.WingetCreateCore.Common.Exceptions;
    using Microsoft.WingetCreateCore.Models;
    using Microsoft.WingetCreateCore.Models.DefaultLocale;
    using Microsoft.WingetCreateCore.Models.Installer;
    using Microsoft.WingetCreateCore.Models.Locale;
    using Microsoft.WingetCreateCore.Models.Version;
    using Sharprompt;

    /// <summary>
    /// Command for updating the elements of an existing or local manifest.
    /// </summary>
    [Verb("update", HelpText = "UpdateCommand_HelpText", ResourceType = typeof(Resources))]
    public class UpdateCommand : BaseCommand
    {
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
        /// Gets or sets a value indicating whether to launch an interactive mode for users to manually select which installers to update.
        /// </summary>
        [Option('i', "interactive", Required = false, HelpText = "InteractiveUpdate_HelpText", ResourceType = typeof(Resources))]
        public bool Interactive { get; set; }

        /// <summary>
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

        /// <summary>
        /// Gets or sets the new value(s) used to update the manifest installer elements.
        /// </summary>
        [Option('u', "urls", Required = false, HelpText = "InstallerUrl_HelpText", ResourceType = typeof(Resources))]
        public IEnumerable<string> InstallerUrls { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the unbound arguments that exist after the first positional parameter.
        /// </summary>
        [Value(1, Hidden = true)]
        public IList<string> UnboundArgs { get; set; } = new List<string>();

        /// <summary>
        /// Executes the update command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(UpdateCommand),
                InstallerUrl = string.Join(',', this.InstallerUrls),
                Id = this.Id,
                Version = this.Version,
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

                Logger.DebugLocalized(nameof(Resources.RetrievingManifest_Message), this.Id);

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
                    latestManifestContent = await this.GitHubClient.GetLatestManifestContentAsync(this.Id);
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
        /// Executes the manifest update flow.
        /// </summary>
        /// <param name="latestManifestContent">List of manifests to be updated.</param>
        /// <param name="commandEvent">CommandExecuted telemetry event.</param>
        /// <returns>Boolean representing whether the manifest was updated successfully or not.</returns>
        public async Task<bool> ExecuteManifestUpdate(List<string> latestManifestContent, CommandExecutedEvent commandEvent)
        {
            Manifests originalManifests = Serialization.DeserializeManifestContents(latestManifestContent);
            Manifests initialManifests = this.DeserializeManifestContentAndApplyInitialUpdate(latestManifestContent);
            Manifests updatedManifests = this.Interactive ?
                await this.UpdateManifestsInteractively(initialManifests) :
                await this.UpdateManifestsAutonomously(initialManifests);

            if (updatedManifests == null)
            {
                return false;
            }

            DisplayManifestPreview(updatedManifests);

            if (string.IsNullOrEmpty(this.OutputDir))
            {
                this.OutputDir = Directory.GetCurrentDirectory();
            }

            string manifestDirectoryPath = SaveManifestDirToLocalPath(updatedManifests, this.OutputDir);

            if (ValidateManifest(manifestDirectoryPath))
            {
                if (this.SubmitToGitHub)
                {
                    if (!VerifyUpdatedInstallerHash(originalManifests, updatedManifests.InstallerManifest))
                    {
                        Logger.ErrorLocalized(nameof(Resources.NoChangeDetectedInUpdatedManifest_Message));
                        Logger.ErrorLocalized(nameof(Resources.CompareUpdatedManifestWithExisting_Message));
                        return false;
                    }

                    return await this.LoadGitHubClient(true)
                        ? (commandEvent.IsSuccessful = await this.GitHubSubmitManifests(updatedManifests))
                        : false;
                }

                return commandEvent.IsSuccessful = true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Updates the Manifest object model with the user-provided installer urls.
        /// </summary>s
        /// <param name="manifests">Manifest object model to be updated.</param>
        /// <returns>Manifests object representing the updates manifest content, or null if the update failed.</returns>
        public async Task<Manifests> UpdateManifestsAutonomously(Manifests manifests)
        {
            InstallerManifest installerManifest = manifests.InstallerManifest;

            if (!this.InstallerUrls.Any())
            {
                this.InstallerUrls = installerManifest.Installers.Select(i => i.InstallerUrl).Distinct().ToArray();
            }

            // We only support updates with same number of installer URLs
            if (this.InstallerUrls.Distinct().Count() != installerManifest.Installers.Select(i => i.InstallerUrl).Distinct().Count())
            {
                Logger.ErrorLocalized(nameof(Resources.MultipleInstallerUpdateDiscrepancy_Error));
                return null;
            }

            var packageFiles = await DownloadInstallers(this.InstallerUrls);
            if (packageFiles == null)
            {
                return null;
            }

            List<PackageParser.DetectedArch> detectedArchOfInstallers;
            List<Installer> newInstallers = new List<Installer>();

            try
            {
                PackageParser.UpdateInstallerNodesAsync(
                    installerManifest,
                    this.InstallerUrls,
                    packageFiles,
                    out detectedArchOfInstallers,
                    out newInstallers);

                DisplayMismatchedArchitectures(detectedArchOfInstallers);
            }
            catch (InvalidOperationException)
            {
                Logger.ErrorLocalized(nameof(Resources.InstallerCountMustMatch_Error));
                return null;
            }
            catch (ParsePackageException parsePackageException)
            {
                parsePackageException.ParseFailedInstallerUrls.ForEach(i => Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error), i));
                return null;
            }
            catch (InstallerMatchException installerMatchException)
            {
                Logger.ErrorLocalized(nameof(Resources.NewInstallerUrlMustMatchExisting_Message));
                installerMatchException.MultipleMatchedInstallers.ForEach(i => Logger.ErrorLocalized(nameof(Resources.UnmatchedInstaller_Error), i.Architecture, i.InstallerType, i.InstallerUrl));
                installerMatchException.UnmatchedInstallers.ForEach(i => Logger.ErrorLocalized(nameof(Resources.MultipleMatchedInstaller_Error), i.Architecture, i.InstallerType, i.InstallerUrl));
                return null;
            }

            return manifests;
        }

        /// <summary>
        /// Deserializes the manifest string content, converts the manifest to multifile,
        /// and ensures package id and version are consistent across the manifests.
        /// </summary>
        /// <param name="latestManifestContent">List of latest manifest content strings.</param>
        /// <returns>Manifests object model.</returns>
        public Manifests DeserializeManifestContentAndApplyInitialUpdate(List<string> latestManifestContent)
        {
            Manifests manifests = Serialization.DeserializeManifestContents(latestManifestContent);

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

            return manifests;
        }

        private static Manifests ConvertSingletonToMultifileManifest(WingetCreateCore.Models.Singleton.SingletonManifest singletonManifest)
        {
            // Create automapping configuration
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, VersionManifest>().ForMember(dest => dest.DefaultLocale, opt => { opt.MapFrom(src => src.PackageLocale); });
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, DefaultLocaleManifest>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, InstallerManifest>();
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

        /// <summary>
        /// Compares the hashes of the original and updated manifests and returns true if the new manifest has an updated SHA256 hash.
        /// </summary>
        /// <param name="oldManifest">The original manifest object model.</param>
        /// <param name="newManifest">The updated installer manifest object model.</param>
        /// <returns>A boolean value indicating whether the updated manifest has new changes compared to the original manifest.</returns>
        private static bool VerifyUpdatedInstallerHash(Manifests oldManifest, InstallerManifest newManifest)
        {
            IEnumerable<string> oldHashes = oldManifest.InstallerManifest == null
                ? oldManifest.SingletonManifest.Installers.Select(i => i.InstallerSha256).Distinct()
                : oldManifest.InstallerManifest.Installers.Select(i => i.InstallerSha256).Distinct();

            var newHashes = newManifest.Installers.Select(i => i.InstallerSha256).Distinct();
            return newHashes.Except(oldHashes).Any();
        }

        /// <summary>
        /// Update flow for interactively updating the manifest.
        /// </summary>s
        /// <param name="manifests">Manifest object model to be updated.</param>
        /// <returns>The updated manifest.</returns>
        private async Task<Manifests> UpdateManifestsInteractively(Manifests manifests)
        {
            Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
            Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

            do
            {
                await this.UpdateInstallersInteractively(manifests.InstallerManifest.Installers);
                DisplayManifestPreview(manifests);
                ValidateManifestsInTempDir(manifests);
            }
            while (Prompt.Confirm(Resources.ConfirmManifestCreation_Message));
            Console.Clear();
            return manifests;
        }

        /// <summary>
        /// Interactive flow when updating installers.
        /// </summary>
        /// <param name="existingInstallers">List of existing installers to be updated.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UpdateInstallersInteractively(List<Installer> existingInstallers)
        {
            var serializer = Serialization.CreateSerializer();
            int numOfExistingInstallers = existingInstallers.Count;
            int index = 1;

            foreach (var installer in existingInstallers)
            {
                Logger.InfoLocalized(nameof(Resources.UpdatingInstallerOutOfTotal_Message), index, numOfExistingInstallers);
                Console.WriteLine(serializer.Serialize(installer));
                await this.UpdateSingleInstallerInteractively(installer);
                index++;
            }
        }

        /// <summary>
        /// Prompts the user for the new installer url to update the provided installer.
        /// </summary>
        /// <param name="installer">The installer to be updated.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UpdateSingleInstallerInteractively(Installer installer)
        {
            Installer newInstaller = new Installer();

            while (true)
            {
                string url = Prompt.Input<string>(Resources.NewInstallerUrl_Message, null, new[] { FieldValidation.ValidateProperty(newInstaller, nameof(Installer.InstallerUrl)) });
                string packageFile = await DownloadPackageFile(url);

                if (string.IsNullOrEmpty(packageFile))
                {
                    continue;
                }
                else if (!PackageParser.ParsePackageAndUpdateInstallerNode(installer, packageFile, url))
                {
                    Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error), url);
                }
                else
                {
                    break;
                }
            }

            Logger.InfoLocalized(nameof(Resources.InstallerUpdatedSuccessfully_Message));
            Console.WriteLine(Resources.PressKeyToContinue_Message);
            Console.ReadKey();
            Console.Clear();
        }
    }
}
