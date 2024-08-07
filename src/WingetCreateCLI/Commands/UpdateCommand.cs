// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoMapper;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;
    using Microsoft.WingetCreateCore.Common;
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
                yield return new Example(Resources.Example_UpdateCommand_SearchAndUpdateVersionAndInstallerURL, new UpdateCommand { Id = "<PackageIdentifier>", InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>" }, Version = "<Version>" });
                yield return new Example(Resources.Example_UpdateCommand_SaveAndPublish, new UpdateCommand { Id = "<PackageIdentifier>", Version = "<Version>", OutputDir = "<OutputDirectory>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_UpdateCommand_OverrideArchitecture, new UpdateCommand { Id = "<PackageIdentifier>", InstallerUrls = new string[] { "'<InstallerUrl1>|<InstallerArchitecture>'" }, Version = "<Version>" });
                yield return new Example(Resources.Example_UpdateCommand_OverrideScope, new UpdateCommand { Id = "<PackageIdentifier>", InstallerUrls = new string[] { "'<InstallerUrl1>|<InstallerScope>'" }, Version = "<Version>" });
                yield return new Example(Resources.Example_UpdateCommand_SubmitToGitHub, new UpdateCommand { Id = "<PackageIdentifier>", Version = "<Version>", InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>" }, SubmitToGitHub = true, GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the previous version to replace from the Windows Package Manager repository.
        /// </summary>
        [Value(1, MetaName = "ReplaceVersion", Required = false, HelpText = "ReplaceVersion_HelpText", ResourceType = typeof(Resources))]
        public string ReplaceVersion { get; set; }

        /// <summary>
        /// Gets or sets the new value used to update the manifest version element.
        /// </summary>
        [Option('v', "version", Required = false, HelpText = "Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the new value used to update the display version field in the manifest.
        /// </summary>
        [Option('d', "display-version", Required = false, HelpText = "DisplayVersion_HelpText", ResourceType = typeof(Resources))]
        public string DisplayVersion { get; set; }

        /// <summary>
        /// Gets or sets the release notes URL for the manifest.
        /// </summary>
        [Option("release-notes-url", Required = false, HelpText = "ReleaseNotesUrl_HelpText", ResourceType = typeof(Resources))]
        public string ReleaseNotesUrl { get; set; }

        /// <summary>
        /// Gets or sets the release date for the manifest.
        /// </summary>
        [Option("release-date", Required = false, HelpText = "ReleaseDate_HelpText", ResourceType = typeof(Resources))]
        public DateTimeOffset? ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the outputPath where the generated manifest file should be saved to.
        /// </summary>
        [Option('o', "out", Required = false, HelpText = "OutputDirectory_HelpText", ResourceType = typeof(Resources))]
        public string OutputDir { get; set; }

        /// <summary>
        /// Gets or sets the title for the pull request.
        /// </summary>
        [Option('p', "prtitle", Required = false, HelpText = "PullRequestTitle_HelpText", ResourceType = typeof(Resources))]
        public override string PRTitle { get => base.PRTitle; set => base.PRTitle = value; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the updated manifest should be submitted to Github.
        /// </summary>
        [Option('s', "submit", Required = false, HelpText = "SubmitToWinget_HelpText", ResourceType = typeof(Resources))]
        public bool SubmitToGitHub { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to replace a previous version of the manifest with the update.
        /// </summary>
        [Option('r', "replace", Required = false, HelpText = "ReplacePrevious_HelpText", ResourceType = typeof(Resources))]
        public bool Replace { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to launch an interactive mode for users to manually select which installers to update.
        /// </summary>
        [Option('i', "interactive", Required = false, HelpText = "InteractiveUpdate_HelpText", ResourceType = typeof(Resources))]
        public bool Interactive { get; set; }

        /// <summary>
        /// Gets or sets the format of the output manifest files.
        /// </summary>
        [Option('f', "format", Required = false, HelpText = "ManifestFormat_HelpText", ResourceType = typeof(Resources))]
        public override ManifestFormat Format { get => base.Format; set => base.Format = value; }

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
        /// Gets or sets the unbound arguments that exist after the positional parameters.
        /// </summary>
        [Value(2, Hidden = true)]
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

                bool submitFlagMissing = !this.SubmitToGitHub && (!string.IsNullOrEmpty(this.PRTitle) || this.Replace);

                if (!string.IsNullOrEmpty(this.ReleaseNotesUrl))
                {
                    Uri uriResult;
                    bool isValid = Uri.TryCreate(this.ReleaseNotesUrl, UriKind.Absolute, out uriResult) &&
                        (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

                    if (!isValid)
                    {
                        Logger.ErrorLocalized(nameof(Resources.SentenceBadFormatConversionErrorOption), nameof(this.ReleaseNotesUrl));
                        return false;
                    }
                }

                if (submitFlagMissing)
                {
                    Logger.WarnLocalized(nameof(Resources.SubmitFlagMissing_Warning));
                    Console.WriteLine();
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

                if (!string.IsNullOrEmpty(this.ReplaceVersion))
                {
                    // If update version is same as replace version, it's a regular update.
                    if (this.Version == this.ReplaceVersion)
                    {
                        Logger.ErrorLocalized(nameof(Resources.ReplaceVersionEqualsUpdateVersion_ErrorMessage));
                        return false;
                    }

                    // Check if the replace version exists in the repository.
                    try
                    {
                        await this.GitHubClient.GetManifestContentAsync(this.Id, this.ReplaceVersion);
                    }
                    catch (Octokit.NotFoundException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.VersionDoesNotExist_Error), this.ReplaceVersion, this.Id);
                        return false;
                    }
                }

                List<string> latestManifestContent;

                try
                {
                    latestManifestContent = await this.GitHubClient.GetManifestContentAsync(this.Id);
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

            RemoveEmptyStringFieldsInManifests(updatedManifests);
            ShiftInstallerFieldsToRootLevel(updatedManifests.InstallerManifest);
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

                    if (!this.Replace)
                    {
                        // Updated manifests will always be in multi-manifest format so no need to check for singleton manifest.
                        string updatedVersion = updatedManifests.VersionManifest.PackageVersion;
                        string originalVersion = originalManifests.VersionManifest != null ? originalManifests.VersionManifest.PackageVersion : originalManifests.SingletonManifest.PackageVersion;

                        if (WinGetUtil.CompareVersions(updatedVersion, originalVersion) != 0 && AreInstallerUrlsVanityUrls(originalManifests, updatedManifests))
                        {
                            Logger.InfoLocalized(nameof(Resources.AutoReplacingPreviousVersion_Message));
                            Console.WriteLine();
                            this.Replace = true;
                        }
                    }

                    return await this.LoadGitHubClient(true)
                        ? (commandEvent.IsSuccessful = await this.GitHubSubmitManifests(
                            updatedManifests,
                            this.GetPRTitle(updatedManifests, originalManifests),
                            this.Replace,
                            this.ReplaceVersion))
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
            ShiftRootFieldsToInstallerLevel(manifests.InstallerManifest);
            InstallerManifest installerManifest = manifests.InstallerManifest;

            if (!this.InstallerUrls.Any())
            {
                this.InstallerUrls = installerManifest.Installers.Select(i => i.InstallerUrl).Distinct().ToArray();
            }

            // Generate list of InstallerUpdate objects and parse out any specified installer URL arguments.
            List<InstallerMetadata> installerMetadataList = this.ParseInstallerUrlsForArguments(this.InstallerUrls.Select(i => i.Trim()).ToList());

            // If the installer update list is null there was an issue when parsing for additional installer arguments.
            if (installerMetadataList == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(this.DisplayVersion))
            {
                // Use --display-version value if version was not provided as an argument.
                foreach (InstallerMetadata installerUpdate in installerMetadataList)
                {
                    if (string.IsNullOrEmpty(installerUpdate.DisplayVersion))
                    {
                        installerUpdate.DisplayVersion = this.DisplayVersion;
                    }
                }
            }

            var originalAppsAndFeaturesEntries = installerManifest.Installers
                .Where(i => i.AppsAndFeaturesEntries != null)
                .SelectMany(i => i.AppsAndFeaturesEntries);

            int originalDisplayVersionCount = originalAppsAndFeaturesEntries
                .Count(entry => entry.DisplayVersion != null);

            int newDisplayVersionCount = installerMetadataList
                .Count(entry => entry.DisplayVersion != null);

            if (newDisplayVersionCount < originalDisplayVersionCount)
            {
                Logger.WarnLocalized(nameof(Resources.UnchangedDisplayVersion_Warning));
            }

            // Check if any single installer has multiple display versions in the original manifest.
            bool installerHasMultipleDisplayVersions = originalAppsAndFeaturesEntries
                .Where(entry => entry.DisplayVersion != null)
                .GroupBy(entry => entry.DisplayVersion)
                .Any(group => group.Count() > 1);

            // It is possible for a single installer to have multiple ARP entries having multiple display versions,
            // but currently, we only take the primary ARP entry in the community repository. If such a case is detected,
            // user will have to manually update the manifest.
            if (installerHasMultipleDisplayVersions)
            {
                Logger.WarnLocalized(nameof(Resources.InstallerWithMultipleDisplayVersions_Warning));
            }

            // Reassign list with parsed installer URLs without installer URL arguments
            this.InstallerUrls = installerMetadataList.Select(x => x.InstallerUrl).ToList();

            foreach (var installerUpdate in installerMetadataList)
            {
                if (installerUpdate.OverrideArchitecture.HasValue)
                {
                    Logger.WarnLocalized(nameof(Resources.OverridingArchitecture_Warning), installerUpdate.InstallerUrl, installerUpdate.OverrideArchitecture);
                }

                if (installerUpdate.OverrideScope.HasValue)
                {
                    Logger.WarnLocalized(nameof(Resources.OverridingScope_Warning), installerUpdate.InstallerUrl, installerUpdate.OverrideScope);
                }
            }

            // We only support updates with same number of installer URLs
            if (this.InstallerUrls.Distinct().Count() != installerManifest.Installers.Select(i => i.InstallerUrl).Distinct().Count())
            {
                Logger.ErrorLocalized(nameof(Resources.MultipleInstallerUpdateDiscrepancy_Error));
                return null;
            }

            foreach (var installerUpdate in installerMetadataList)
            {
                string packageFile = await DownloadPackageFile(installerUpdate.InstallerUrl);
                if (string.IsNullOrEmpty(packageFile))
                {
                    return null;
                }

                if (packageFile.IsZipFile())
                {
                    installerUpdate.IsZipFile = true;

                    // Obtain all nested installer files from the previous manifest.
                    List<NestedInstallerFile> nestedInstallerFiles = installerManifest.Installers.SelectMany(i => i.NestedInstallerFiles ?? Enumerable.Empty<NestedInstallerFile>()).ToList();
                    string extractDirectory = ExtractArchiveAndRetrieveDirectoryPath(packageFile);
                    installerUpdate.NestedInstallerFiles = new List<NestedInstallerFile>();

                    foreach (NestedInstallerFile nestedInstallerFile in nestedInstallerFiles)
                    {
                        // If the previous RelativeFilePath is not an exact match, check if there is a new suitable match in the archive.
                        if (!File.Exists(Path.Combine(extractDirectory, nestedInstallerFile.RelativeFilePath)))
                        {
                            string relativeFilePath = this.ObtainMatchingRelativeFilePath(nestedInstallerFile.RelativeFilePath, extractDirectory, packageFile);
                            if (string.IsNullOrEmpty(relativeFilePath))
                            {
                                return null;
                            }

                            List<string> installerUpdateRelativeFilePathList = installerUpdate.NestedInstallerFiles.Select(x => x.RelativeFilePath).ToList();

                            // Skip if the relative path is already in the installer update list.
                            if (!installerUpdateRelativeFilePathList.Contains(relativeFilePath))
                            {
                                installerUpdate.NestedInstallerFiles.Add(new NestedInstallerFile
                                {
                                    RelativeFilePath = relativeFilePath,
                                    PortableCommandAlias = nestedInstallerFile.PortableCommandAlias,
                                });
                            }
                        }
                        else
                        {
                            installerUpdate.NestedInstallerFiles.Add(new NestedInstallerFile
                            {
                                RelativeFilePath = nestedInstallerFile.RelativeFilePath,
                                PortableCommandAlias = nestedInstallerFile.PortableCommandAlias,
                            });
                        }
                    }

                    installerUpdate.ExtractedDirectory = extractDirectory;
                }

                installerUpdate.PackageFile = packageFile;
            }

            try
            {
                PackageParser.UpdateInstallerNodesAsync(installerMetadataList, installerManifest);
                DisplayArchitectureWarnings(installerMetadataList);
                ResetVersionSpecificFields(manifests);
                try
                {
                    Logger.InfoLocalized(nameof(Resources.PopulatingGitHubMetadata_Message));

                    if (this.GitHubClient != null)
                    {
                        await this.GitHubClient.PopulateGitHubMetadata(manifests, this.Format.ToString());
                    }
                }
                catch (Octokit.ApiException)
                {
                    // Print a warning, but continue with the update.
                    Logger.ErrorLocalized(nameof(Resources.CouldNotPopulateGitHubMetadata_Warning));
                }

                this.AddVersionSpecificMetadata(manifests);
                ShiftInstallerFieldsToRootLevel(manifests.InstallerManifest);
            }
            catch (InvalidOperationException)
            {
                Logger.ErrorLocalized(nameof(Resources.InstallerCountMustMatch_Error));
                return null;
            }
            catch (IOException iOException) when (iOException.HResult == -2147024671)
            {
                Logger.ErrorLocalized(nameof(Resources.DefenderVirus_ErrorMessage));
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

                if (installerMatchException.IsArchitectureOverride)
                {
                    Logger.WarnLocalized(nameof(Resources.ArchitectureOverride_Warning));
                }
                else
                {
                    Logger.WarnLocalized(nameof(Resources.UseOverrides_ErrorMessage));
                }

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

            EnsureManifestVersionConsistency(manifests);

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

        private static string ExtractArchiveAndRetrieveDirectoryPath(string packageFilePath)
        {
            string extractDirectory = Path.Combine(PackageParser.InstallerDownloadPath, Path.GetFileNameWithoutExtension(packageFilePath));

            if (Directory.Exists(extractDirectory))
            {
                Directory.Delete(extractDirectory, true);
            }

            try
            {
                ZipFile.ExtractToDirectory(packageFilePath, extractDirectory, true);
                return extractDirectory;
            }
            catch (Exception ex)
            {
                if (ex is InvalidDataException || ex is IOException || ex is NotSupportedException)
                {
                    Logger.ErrorLocalized(nameof(Resources.InvalidZipFile_ErrorMessage), ex);
                    return null;
                }
                else if (ex is PathTooLongException)
                {
                    Logger.ErrorLocalized(nameof(Resources.ZipPathExceedsMaxLength_ErrorMessage), ex);
                    return null;
                }

                throw;
            }
        }

        private static Manifests ConvertSingletonToMultifileManifest(WingetCreateCore.Models.Singleton.SingletonManifest singletonManifest)
        {
            // Create automapping configuration
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullCollections = true;
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, VersionManifest>()
                    .ForMember(dest => dest.DefaultLocale, opt => opt.MapFrom(src => src.PackageLocale))
                    .ForMember(dest => dest.ManifestVersion, opt => opt.Ignore());
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, DefaultLocaleManifest>().ForMember(dest => dest.ManifestVersion, opt => opt.Ignore());
                cfg.CreateMap<WingetCreateCore.Models.Singleton.SingletonManifest, InstallerManifest>()
                    .ForMember(dest => dest.ManifestVersion, opt => opt.Ignore());
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Dependencies, WingetCreateCore.Models.Installer.Dependencies>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Installer, WingetCreateCore.Models.Installer.Installer>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.InstallerSwitches, WingetCreateCore.Models.Installer.InstallerSwitches>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.AppsAndFeaturesEntry, WingetCreateCore.Models.Installer.AppsAndFeaturesEntry>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.ExpectedReturnCode, WingetCreateCore.Models.Installer.ExpectedReturnCode>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.PackageDependencies, WingetCreateCore.Models.Installer.PackageDependencies>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Markets, WingetCreateCore.Models.Installer.Markets>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Markets2, WingetCreateCore.Models.Installer.Markets2>(); // Markets2 is not used, but is required to satisfy mapping configuration.
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Agreement, WingetCreateCore.Models.DefaultLocale.Agreement>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Documentation, WingetCreateCore.Models.DefaultLocale.Documentation>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.NestedInstallerFile, WingetCreateCore.Models.Installer.NestedInstallerFile>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Files, WingetCreateCore.Models.Installer.Files>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.InstallationMetadata, WingetCreateCore.Models.Installer.InstallationMetadata>();
                cfg.CreateMap<WingetCreateCore.Models.Singleton.Icon, WingetCreateCore.Models.DefaultLocale.Icon>();
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
        /// Resets the value of version specific fields to null.
        /// </summary>
        /// <param name="manifests">Manifests object model.</param>
        private static void ResetVersionSpecificFields(Manifests manifests)
        {
            DefaultLocaleManifest defaultLocaleManifest = manifests.DefaultLocaleManifest;
            InstallerManifest installerManifest = manifests.InstallerManifest;
            List<LocaleManifest> localeManifests = manifests.LocaleManifests;

            defaultLocaleManifest.ReleaseNotes = null;
            defaultLocaleManifest.ReleaseNotesUrl = null;

            installerManifest.ReleaseDateTime = null;
            installerManifest.ReleaseDate = null;
            foreach (var installer in installerManifest.Installers)
            {
                installer.ReleaseDateTime = null;
                installer.ReleaseDate = null;
            }

            foreach (LocaleManifest localeManifest in localeManifests)
            {
                localeManifest.ReleaseNotes = null;
                localeManifest.ReleaseNotesUrl = null;
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

        private static void DisplayManifestsAsMenuSelection(Manifests manifests)
        {
            Console.Clear();
            string versionFileName = Manifests.GetFileName(manifests.VersionManifest, Extension);
            string installerFileName = Manifests.GetFileName(manifests.InstallerManifest, Extension);
            string versionManifestMenuItem = $"{manifests.VersionManifest.ManifestType.ToUpper()}: " + versionFileName;
            string installerManifestMenuItem = $"{manifests.InstallerManifest.ManifestType.ToUpper()}: " + installerFileName;

            while (true)
            {
                // Need to update locale manifest file name each time as PackageLocale can change
                string defaultLocaleMenuItem = $"{manifests.DefaultLocaleManifest.ManifestType.ToUpper()}: " + Manifests.GetFileName(manifests.DefaultLocaleManifest, Extension);
                List<string> selectionList = new List<string> { versionManifestMenuItem, installerManifestMenuItem, defaultLocaleMenuItem };
                Dictionary<string, LocaleManifest> localeManifestMap = new Dictionary<string, LocaleManifest>();
                foreach (LocaleManifest localeManifest in manifests.LocaleManifests)
                {
                    string localeManifestFileName = $"{localeManifest.ManifestType.ToUpper()}: " + Manifests.GetFileName(localeManifest, Extension);
                    localeManifestMap.Add(localeManifestFileName, localeManifest);
                    selectionList.Add(localeManifestFileName);
                }

                selectionList.Add(Resources.Done_MenuItem);
                ValidateManifestsInTempDir(manifests);
                var selectedItem = Prompt.Select(Resources.SelectManifestToEdit_Message, selectionList);

                if (selectedItem == versionManifestMenuItem)
                {
                    PromptHelper.PromptPropertiesWithMenu(manifests.VersionManifest, Resources.SaveAndExit_MenuItem, versionFileName);
                }
                else if (selectedItem == installerManifestMenuItem)
                {
                    PromptHelper.PromptPropertiesWithMenu(manifests.InstallerManifest, Resources.SaveAndExit_MenuItem, installerFileName);
                }
                else if (selectedItem == defaultLocaleMenuItem)
                {
                    PromptHelper.PromptPropertiesWithMenu(manifests.DefaultLocaleManifest, Resources.SaveAndExit_MenuItem, Manifests.GetFileName(manifests.DefaultLocaleManifest, Extension));
                }
                else if (selectedItem == Resources.Done_MenuItem)
                {
                    break;
                }
                else
                {
                    var selectedLocaleManifest = localeManifestMap[selectedItem];
                    PromptHelper.PromptPropertiesWithMenu(selectedLocaleManifest, Resources.SaveAndExit_MenuItem, Manifests.GetFileName(selectedLocaleManifest, Extension));
                }
            }
        }

        private static bool AreInstallerUrlsVanityUrls(Manifests baseManifest, Manifests newManifest)
        {
            List<Installer> newInstallers = newManifest.InstallerManifest.Installers;

            // All installer URLs in the new manifest must have a matching installer URL in the base manifest.
            foreach (Installer installer in newInstallers)
            {
                if (baseManifest.InstallerManifest != null && !baseManifest.InstallerManifest.Installers.Any(i => i.InstallerUrl == installer.InstallerUrl))
                {
                    return false;
                }

                if (baseManifest.SingletonManifest != null && !baseManifest.SingletonManifest.Installers.Any(i => i.InstallerUrl == installer.InstallerUrl))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddVersionSpecificMetadata(Manifests updatedManifests)
        {
            if (this.ReleaseDate != null)
            {
                switch (this.Format)
                {
                    case ManifestFormat.Yaml:
                        updatedManifests.InstallerManifest.ReleaseDateTime = this.ReleaseDate.Value.ToString("yyyy-MM-dd");
                        break;
                    case ManifestFormat.Json:
                        updatedManifests.InstallerManifest.ReleaseDate = this.ReleaseDate;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(this.ReleaseNotesUrl))
            {
                updatedManifests.DefaultLocaleManifest.ReleaseNotesUrl = this.ReleaseNotesUrl;
            }
        }

        private string ObtainMatchingRelativeFilePath(string oldRelativeFilePath, string directory, string archiveName)
        {
            string fileName = Path.GetFileName(oldRelativeFilePath);
            List<string> matchingFiles = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories)
                .Select(filePath => filePath = Path.GetRelativePath(directory, filePath))
                .ToList();

            // If there is only one match in the archive, use that as the new relative file path.
            if (matchingFiles.Count == 1)
            {
                return matchingFiles.First();
            }
            else if (matchingFiles.Count > 1)
            {
                if (this.Interactive)
                {
                    Console.WriteLine();
                    Logger.WarnLocalized(nameof(Resources.MultipleMatchingNestedInstallersFound_Warning), fileName, Path.GetFileName(archiveName));
                    return Prompt.Select(Resources.SelectRelativeFilePath_Message, matchingFiles);
                }

                Logger.ErrorLocalized(nameof(Resources.MultipleMatchingNestedInstallersFound_Error), fileName, Path.GetFileName(archiveName));
                return null;
            }
            else
            {
                if (this.Interactive)
                {
                    List<string> sameExtensionFiles = Directory.GetFiles(directory, $"*{Path.GetExtension(oldRelativeFilePath)}", SearchOption.AllDirectories)
                        .Select(filePath => filePath = Path.GetRelativePath(directory, filePath))
                        .ToList();
                    if (sameExtensionFiles.Count > 0)
                    {
                        Console.WriteLine();
                        Logger.WarnLocalized(nameof(Resources.NestedInstallerFileNotFound_Warning), oldRelativeFilePath);
                        return Prompt.Select(Resources.SelectRelativeFilePath_Message, sameExtensionFiles);
                    }
                    else
                    {
                        return null;
                    }
                }

                Logger.ErrorLocalized(nameof(Resources.NestedInstallerFileNotFound_Error), oldRelativeFilePath);
                return null;
            }
        }

        /// <summary>
        /// Parses the installer urls for any additional arguments.
        /// </summary>
        /// <param name="installerUrlsToBeParsed">List of installer URLs to be parsed for additional arguments.</param>
        /// <returns>List of <see cref="InstallerMetadata"/> helper objects used for updating the installers.</returns>
        private List<InstallerMetadata> ParseInstallerUrlsForArguments(List<string> installerUrlsToBeParsed)
        {
            // There can be at most 4 elements at one time (installerUrl|archOverride|scopeOverride|displayVersion)
            const int MaxUrlArgumentLimit = 4;

            List<InstallerMetadata> installerMetadataList = new List<InstallerMetadata>();
            foreach (string item in installerUrlsToBeParsed)
            {
                InstallerMetadata installerMetadata = new InstallerMetadata();

                if (item.Contains('|'))
                {
                    // '|' character indicates that user is providing additional arguments for the installer URL.
                    string[] installerUrlArguments = item.Split('|');

                    if (installerUrlArguments.Length > MaxUrlArgumentLimit)
                    {
                        Logger.ErrorLocalized(nameof(Resources.ArgumentLimitExceeded_Error), item);
                        return null;
                    }

                    installerMetadata.InstallerUrl = installerUrlArguments[0];

                    bool archOverridePresent = false;
                    bool scopeOverridePresent = false;
                    bool displayVersionPresent = false;

                    for (int i = 1; i < installerUrlArguments.Length; i++)
                    {
                        string argumentString = installerUrlArguments[i];
                        Architecture? overrideArch = argumentString.ToEnumOrDefault<Architecture>();
                        Scope? overrideScope = argumentString.ToEnumOrDefault<Scope>();

                        if (overrideArch.HasValue)
                        {
                            if (archOverridePresent)
                            {
                                Logger.ErrorLocalized(nameof(Resources.MultipleArchitectureOverride_Error));
                                return null;
                            }
                            else
                            {
                                archOverridePresent = true;
                                installerMetadata.OverrideArchitecture = overrideArch.Value;
                            }
                        }
                        else if (overrideScope.HasValue)
                        {
                            if (scopeOverridePresent)
                            {
                                Logger.ErrorLocalized(nameof(Resources.MultipleScopeOverride_Error));
                                return null;
                            }
                            else
                            {
                                scopeOverridePresent = true;
                                installerMetadata.OverrideScope = overrideScope.Value;
                            }
                        }

                        // If value is not a convertible enum, it is assumed to be a display version.
                        else if (!string.IsNullOrEmpty(argumentString) && !displayVersionPresent)
                        {
                            displayVersionPresent = true;
                            installerMetadata.DisplayVersion = argumentString;
                        }
                        else
                        {
                            Logger.ErrorLocalized(nameof(Resources.UnableToParseArgument_Error), argumentString);
                            return null;
                        }
                    }
                }
                else
                {
                    installerMetadata.InstallerUrl = item;
                }

                installerMetadataList.Add(installerMetadata);
            }

            return installerMetadataList;
        }

        /// <summary>
        /// Update flow for interactively updating the manifest.
        /// </summary>s
        /// <param name="manifests">Manifest object model to be updated.</param>
        /// <returns>The updated manifest.</returns>
        private async Task<Manifests> UpdateManifestsInteractively(Manifests manifests)
        {
            ShiftRootFieldsToInstallerLevel(manifests.InstallerManifest);
            Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
            Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

            // Clone the list of installers in order to preserve initial values.
            Manifests originalManifest = new Manifests { InstallerManifest = new InstallerManifest() };
            originalManifest.InstallerManifest.Installers = manifests.CloneInstallers();

            do
            {
                Console.Clear();
                manifests.InstallerManifest.Installers = originalManifest.CloneInstallers();
                await this.UpdateInstallersInteractively(manifests.InstallerManifest.Installers);
                ShiftInstallerFieldsToRootLevel(manifests.InstallerManifest);
                ResetVersionSpecificFields(manifests);
                try
                {
                    Logger.InfoLocalized(nameof(Resources.PopulatingGitHubMetadata_Message));
                    if (this.GitHubClient != null)
                    {
                        await this.GitHubClient.PopulateGitHubMetadata(manifests, this.Format.ToString());
                    }
                }
                catch (Octokit.ApiException)
                {
                    // Print a warning, but continue with the update.
                    Logger.ErrorLocalized(nameof(Resources.CouldNotPopulateGitHubMetadata_Warning));
                }

                this.AddVersionSpecificMetadata(manifests);
                DisplayManifestPreview(manifests);
                ValidateManifestsInTempDir(manifests);
            }
            while (Prompt.Confirm(Resources.DiscardUpdateAndStartOver_Message));

            if (Prompt.Confirm(Resources.EditManifests_Message))
            {
                DisplayManifestsAsMenuSelection(manifests);
            }

            if (!this.SubmitToGitHub)
            {
                this.SubmitToGitHub = Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message);
            }

            return manifests;
        }

        /// <summary>
        /// Interactive flow when updating installers.
        /// </summary>
        /// <param name="existingInstallers">List of existing installers to be updated.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task UpdateInstallersInteractively(List<Installer> existingInstallers)
        {
            int numOfExistingInstallers = existingInstallers.Count;
            int index = 1;

            foreach (var installer in existingInstallers)
            {
                Logger.InfoLocalized(nameof(Resources.UpdatingInstallerOutOfTotal_Message), index, numOfExistingInstallers);
                Console.WriteLine(Serialization.Serialize(installer));
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
                string url = Prompt.Input<string>(Resources.NewInstallerUrl_Message, null, null, new[] { FieldValidation.ValidateProperty(newInstaller, nameof(Installer.InstallerUrl)) });

                string packageFile = await DownloadPackageFile(url);
                string archivePath = null;

                if (string.IsNullOrEmpty(packageFile))
                {
                    continue;
                }

                if (packageFile.IsZipFile())
                {
                    archivePath = packageFile;
                    string extractDirectory = ExtractArchiveAndRetrieveDirectoryPath(packageFile);
                    bool isRelativePathNull = false;

                    foreach (NestedInstallerFile nestedInstaller in installer.NestedInstallerFiles)
                    {
                        nestedInstaller.RelativeFilePath = this.ObtainMatchingRelativeFilePath(nestedInstaller.RelativeFilePath, extractDirectory, packageFile);
                        if (string.IsNullOrEmpty(nestedInstaller.RelativeFilePath))
                        {
                            isRelativePathNull = true;
                            break;
                        }
                    }

                    if (isRelativePathNull)
                    {
                        Logger.ErrorLocalized(nameof(Resources.NestedInstallerTypeNotFound_Error));
                        continue;
                    }

                    packageFile = Path.Combine(extractDirectory, installer.NestedInstallerFiles.First().RelativeFilePath);
                }

                if (!PackageParser.ParsePackageAndUpdateInstallerNode(installer, packageFile, url, archivePath))
                {
                    Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error), url);
                    Console.WriteLine();
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
