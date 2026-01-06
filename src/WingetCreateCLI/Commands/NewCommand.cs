// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
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
    using Microsoft.WingetCreateCore.Models.Version;
    using Sharprompt;

    /// <summary>
    /// Command to launch a wizard that prompt users for information to generate a new manifest.
    /// </summary>
    [Verb("new", HelpText = "NewCommand_HelpText", ResourceType = typeof(Resources))]
    public class NewCommand : BaseCommand
    {
        /// <summary>
        /// Installer types for which we can trust that the detected architecture is correct, so don't need to prompt the user to confirm.
        /// </summary>
        private static readonly InstallerType[] ReliableArchitectureInstallerTypes = new[] { InstallerType.Msix, InstallerType.Appx };

        /// <summary>
        /// Installer type file extensions that are supported.
        /// </summary>
        private static readonly string[] SupportedInstallerTypeExtensions = new[] { ".msix", ".msi", ".exe", ".msixbundle", ".appx", ".appxbundle" };

        /// <summary>
        /// Gets the usage examples for the New command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_NewCommand_StartFromScratch, new NewCommand { });
                yield return new Example(Resources.Example_NewCommand_DownloadInstaller, new NewCommand { InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>, .." } });
                yield return new Example(Resources.Example_NewCommand_SaveLocallyOrSubmit, new NewCommand
                {
                    InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>, ..." },
                    OutputDir = "<OutputDirectory>",
                    GitHubToken = "<GitHubPersonalAccessToken>",
                });
            }
        }

        /// <summary>
        /// Gets or sets the installer URL(s) used for downloading and parsing the installer file(s).
        /// </summary>
        [Value(0, MetaName = "urls", Required = false, HelpText = "InstallerUrl_HelpText", ResourceType = typeof(Resources))]
        public IEnumerable<string> InstallerUrls { get; set; }

        /// <summary>
        /// Gets or sets the outputPath where the generated manifest file should be saved to.
        /// </summary>
        [Option('o', "out", Required = false, HelpText = "OutputDirectory_HelpText", ResourceType = typeof(Resources))]
        public string OutputDir { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to allow unsecure downloads.
        /// </summary>
        [Option("allow-unsecure-downloads", Required = false, HelpText = "AllowUnsecureDownloads_HelpText", ResourceType = typeof(Resources))]
        public bool AllowUnsecureDownloads { get; set; }

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
        /// Gets or sets a value indicating whether the PR should be opened automatically in the browser.
        /// </summary>
        [Option('n', "no-open", Required = false, HelpText = "NoOpenPRInBrowser_HelpText", ResourceType = typeof(Resources))]
        public bool NoOpenPRInBrowser { get => !this.OpenPRInBrowser; set => this.OpenPRInBrowser = !value; }

        /// <summary>
        /// Executes the new command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(NewCommand),
                InstallerUrl = string.Join(',', this.InstallerUrls),
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            try
            {
                Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
                Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

                Manifests manifests = new Manifests();

                if (!this.InstallerUrls.Any())
                {
                    PromptHelper.PromptPropertyAndSetValue(this, nameof(this.InstallerUrls), this.InstallerUrls, minimum: 1, validationModel: new Installer(), validationName: nameof(Installer.InstallerUrl));
                    Console.Clear();
                }

                List<InstallerMetadata> installerUpdateList = new List<InstallerMetadata>();

                foreach (var installerUrl in this.InstallerUrls)
                {
                    string packageFile = await DownloadPackageFile(installerUrl, this.AllowUnsecureDownloads);
                    if (string.IsNullOrEmpty(packageFile))
                    {
                        return false;
                    }

                    if (packageFile.IsZipFile())
                    {
                        string extractDirectory = Path.Combine(PackageParser.InstallerDownloadPath, Path.GetFileNameWithoutExtension(packageFile));

                        if (Directory.Exists(extractDirectory))
                        {
                            Directory.Delete(extractDirectory, true);
                        }

                        try
                        {
                            ZipFile.ExtractToDirectory(packageFile, extractDirectory, true);
                        }
                        catch (Exception ex)
                        {
                            if (ex is InvalidDataException || ex is IOException || ex is NotSupportedException)
                            {
                                Logger.ErrorLocalized(nameof(Resources.InvalidZipFile_ErrorMessage), ex);
                                return false;
                            }
                            else if (ex is PathTooLongException)
                            {
                                Logger.ErrorLocalized(nameof(Resources.ZipPathExceedsMaxLength_ErrorMessage), ex);
                                return false;
                            }

                            throw;
                        }

                        List<string> extractedFiles = Directory.EnumerateFiles(extractDirectory, "*.*", SearchOption.AllDirectories)
                            .Select(filePath => filePath = Path.GetRelativePath(extractDirectory, filePath))
                            .Where(filePath => SupportedInstallerTypeExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant()))
                            .ToList();

                        int extractedFilesCount = extractedFiles.Count();
                        List<string> selectedInstallers;

                        if (extractedFilesCount == 0)
                        {
                            Logger.ErrorLocalized(nameof(Resources.NoInstallersFoundInArchive_ErrorMessage));
                            return false;
                        }
                        else if (extractedFilesCount == 1)
                        {
                            selectedInstallers = extractedFiles;
                        }
                        else
                        {
                            selectedInstallers = Prompt.MultiSelect(Resources.SelectInstallersFromZip_Message, extractedFiles, minimum: 1).ToList();
                        }

                        foreach (var installer in selectedInstallers)
                        {
                            installerUpdateList.Add(
                            new InstallerMetadata
                            {
                                InstallerUrl = installerUrl,
                                PackageFile = packageFile,
                                NestedInstallerFiles = new List<NestedInstallerFile> { new NestedInstallerFile { RelativeFilePath = installer } },
                                IsZipFile = true,
                                ExtractedDirectory = extractDirectory,
                            });
                        }
                    }
                    else
                    {
                        installerUpdateList.Add(new InstallerMetadata { InstallerUrl = installerUrl, PackageFile = packageFile });
                    }
                }

                try
                {
                    PackageParser.ParsePackages(installerUpdateList, manifests);

                    // The CLI parses ARP entries uses them in update flow to update existing AppsAndFeaturesEntries in the manifest.
                    // AppsAndFeaturesEntries should not be set for a new package as they may cause more harm than good.
                    RemoveARPEntries(manifests.InstallerManifest);
                    DisplayArchitectureWarnings(installerUpdateList);
                }
                catch (IOException iOException) when (iOException.HResult == -2147024671)
                {
                    Logger.ErrorLocalized(nameof(Resources.DefenderVirus_ErrorMessage));
                    return false;
                }
                catch (ParsePackageException parsePackageException)
                {
                    parsePackageException.ParseFailedInstallerUrls.ForEach(i => Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error), i));
                    return false;
                }

                Console.WriteLine();
                Console.WriteLine(Resources.NewCommand_Header);
                Console.WriteLine();
                Logger.InfoLocalized(nameof(Resources.ManifestDocumentation_HelpText), Constants.ManifestDocumentationUrl);
                Console.WriteLine();
                Console.WriteLine(Resources.NewCommand_PrePrompt_Header);
                Console.WriteLine();

                Logger.DebugLocalized(nameof(Resources.EnterFollowingFields_Message));

                bool isManifestValid;

                do
                {
                    this.PromptPackageIdentifier(manifests);

                    if (this.WingetRepoOwner == DefaultWingetRepoOwner && this.WingetRepo == DefaultWingetRepo)
                    {
                        if (await this.IsDuplicatePackageIdentifier(manifests.VersionManifest.PackageIdentifier))
                        {
                            Console.WriteLine();
                            Logger.ErrorLocalized(nameof(Resources.PackageIdAlreadyExists_Error));
                            return false;
                        }
                    }

                    ShiftRootFieldsToInstallerLevel(manifests.InstallerManifest);
                    try
                    {
                        if (this.GitHubClient != null)
                        {
                            bool populated = await this.GitHubClient.PopulateGitHubMetadata(manifests, this.Format.ToString());
                            if (populated)
                            {
                                Logger.InfoLocalized(nameof(Resources.PopulatedGitHubMetadata_Message));
                            }
                        }
                    }
                    catch (Octokit.ApiException)
                    {
                        // Print a warning, but continue with the command flow.
                        Logger.ErrorLocalized(nameof(Resources.CouldNotPopulateGitHubMetadata_Warning));
                    }

                    PromptManifestProperties(manifests);
                    MergeNestedInstallerFilesIfApplicable(manifests.InstallerManifest);
                    ShiftInstallerFieldsToRootLevel(manifests.InstallerManifest);
                    RemoveEmptyStringAndListFieldsInManifests(manifests);
                    DisplayManifestPreview(manifests);
                    isManifestValid = ValidateManifestsInTempDir(manifests, this.Format);
                }
                while (Prompt.Confirm(Resources.ConfirmManifestCreation_Message));

                if (string.IsNullOrEmpty(this.OutputDir))
                {
                    this.OutputDir = Directory.GetCurrentDirectory();
                }

                SaveManifestDirToLocalPath(manifests, this.OutputDir);

                if (isManifestValid && Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message))
                {
                    if (await this.LoadGitHubClient(true))
                    {
                        return commandEvent.IsSuccessful = await this.GitHubSubmitManifests(
                            manifests,
                            this.GetPRTitle(manifests));
                    }

                    return false;
                }
                else
                {
                    Console.WriteLine();
                    Logger.WarnLocalized(nameof(Resources.SkippingPullRequest_Message));
                    return commandEvent.IsSuccessful = isManifestValid;
                }
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }

        private static void PromptManifestProperties(Manifests manifests)
        {
            PromptRequiredProperties(manifests.VersionManifest);
            PromptRequiredProperties(manifests.InstallerManifest, manifests.VersionManifest);
            PromptRequiredProperties(manifests.DefaultLocaleManifest, manifests.VersionManifest);

            Console.WriteLine();
            if (Prompt.Confirm(Resources.ModifyOptionalDefaultLocaleFields_Message))
            {
                PromptOptionalProperties(manifests.DefaultLocaleManifest);
            }

            if (Prompt.Confirm(Resources.ModifyOptionalInstallerFields_Message))
            {
                PromptHelper.DisplayInstallersAsMenuSelection(manifests.InstallerManifest);
            }

            Console.WriteLine();
        }

        private static void PromptRequiredProperties<T>(T manifest, VersionManifest versionManifest = null)
        {
            var properties = manifest.GetType().GetProperties().ToList();
            var requiredProperties = properties.Where(p => p.GetCustomAttribute<RequiredAttribute>() != null).ToList();

            foreach (var property in requiredProperties)
            {
                if (property.PropertyType.IsGenericType)
                {
                    // Generic logic for handling nested object models
                    Type itemType = property.GetValue(manifest).GetType().GetGenericArguments().Single();

                    if (itemType.Name == nameof(Installer))
                    {
                        PromptInstallerProperties(manifest, property);
                    }
                }
                else if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    if (property.Name == nameof(VersionManifest.PackageIdentifier) ||
                        property.Name == nameof(VersionManifest.ManifestType) ||
                        property.Name == nameof(VersionManifest.ManifestVersion))
                    {
                        continue;
                    }

                    if (property.Name == nameof(VersionManifest.PackageVersion) && versionManifest != null)
                    {
                        property.SetValue(manifest, versionManifest.PackageVersion);
                        continue;
                    }

                    if (property.Name == nameof(DefaultLocaleManifest.PackageLocale) && versionManifest != null)
                    {
                        property.SetValue(manifest, versionManifest.DefaultLocale);
                        continue;
                    }

                    PromptHelper.PromptPropertyAndSetValue(manifest, property.Name, property.GetValue(manifest));
                    Logger.Trace($"Property [{property.Name}] set to the value [{property.GetValue(manifest)}]");
                }
            }
        }

        private static void PromptInstallerProperties<T>(T manifest, PropertyInfo property)
        {
            List<Installer> installers = new List<Installer>((ICollection<Installer>)property.GetValue(manifest));
            foreach (var installer in installers)
            {
                Console.WriteLine();
                if (installer.InstallerType == InstallerType.Zip)
                {
                    Logger.InfoLocalized(nameof(Resources.NestedInstallerParsing_HelpText), installer.NestedInstallerFiles.First().RelativeFilePath, installer.InstallerUrl);
                }

                var installerProperties = installer.GetType().GetProperties().ToList();

                var requiredInstallerProperties = installerProperties
                    .Where(p => Attribute.IsDefined(p, typeof(RequiredAttribute)) || p.Name == nameof(InstallerType)).ToList();

                bool prompted = false;

                // If the installerType is EXE, prompt the user for whether the package is a portable
                if (installer.InstallerType == InstallerType.Exe || installer.NestedInstallerType == NestedInstallerType.Exe)
                {
                    if (!PromptForPortableExe(installer))
                    {
                        // If we know the installertype is EXE, prompt the user for installer switches (silent and silentwithprogress)
                        Logger.DebugLocalized(nameof(Resources.AdditionalMetadataNeeded_Message), installer.InstallerUrl);
                        prompted = true;
                        PromptInstallerSwitchesForExe(installer);
                    }
                }

                PromptForPortableFieldsIfApplicable(installer);
                foreach (var requiredProperty in requiredInstallerProperties)
                {
                    var currentValue = requiredProperty.GetValue(installer);

                    // Only prompt if the value isn't already set, or if it's the Architecture property and we don't trust the parser to have gotten it correct for this InstallerType.
                    if (currentValue == null ||
                        (requiredProperty.Name == nameof(Installer.Architecture) && !ReliableArchitectureInstallerTypes.Contains(installer.InstallerType.Value)))
                    {
                        if (!prompted)
                        {
                            Console.WriteLine();
                            Logger.DebugLocalized(nameof(Resources.AdditionalMetadataNeeded_Message), installer.InstallerUrl);
                            prompted = true;
                        }

                        PromptHelper.PromptPropertyAndSetValue(installer, requiredProperty.Name, requiredProperty.GetValue(installer));
                    }
                }
            }
        }

        /// <summary>
        /// Prompts the user to confirm whether the package is a portable.
        /// </summary>
        /// <returns>Boolean value indicating whether the package is a portable.</returns>
        private static bool PromptForPortableExe(Installer manifestInstaller)
        {
            if (Prompt.Confirm(Resources.ConfirmPortablePackage_Message))
            {
                if (manifestInstaller.InstallerType == InstallerType.Zip)
                {
                    manifestInstaller.NestedInstallerType = NestedInstallerType.Portable;
                }
                else
                {
                    manifestInstaller.InstallerType = InstallerType.Portable;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private static void PromptInstallerSwitchesForExe<T>(T manifestInstaller)
        {
            InstallerSwitches installerSwitches = new InstallerSwitches();

            PromptHelper.PromptPropertyAndSetValue(installerSwitches, nameof(InstallerSwitches.Silent), installerSwitches.Silent);
            PromptHelper.PromptPropertyAndSetValue(installerSwitches, nameof(InstallerSwitches.SilentWithProgress), installerSwitches.SilentWithProgress);

            if (!string.IsNullOrEmpty(installerSwitches.Silent) || !string.IsNullOrEmpty(installerSwitches.SilentWithProgress))
            {
                manifestInstaller.GetType().GetProperty(nameof(InstallerSwitches)).SetValue(manifestInstaller, installerSwitches);
            }
        }

        private static void PromptForPortableFieldsIfApplicable(Installer installer)
        {
            if (installer.InstallerType == InstallerType.Portable)
            {
                string portableCommandAlias = Prompt.Input<string>(Resources.PortableCommandAlias_Message);

                if (!string.IsNullOrEmpty(portableCommandAlias))
                {
                    List<string> portableCommands = new List<string> { portableCommandAlias.Trim() };
                    installer.Commands = portableCommands;
                }
            }

            if (installer.NestedInstallerType == NestedInstallerType.Portable)
            {
                string portableCommandAlias = Prompt.Input<string>(Resources.PortableCommandAlias_Message);

                if (!string.IsNullOrEmpty(portableCommandAlias))
                {
                    installer.NestedInstallerFiles.First().PortableCommandAlias = portableCommandAlias.Trim();
                }

                // No need to set explicitly in else case as WinGet CLI defaults to using false
                if (Prompt.Confirm(Resources.ConfirmZippedBinary_Message))
                {
                    installer.ArchiveBinariesDependOnPath = true;
                }
            }
        }

        /// <summary>
        /// Merge nested installer files into a single installer if:
        /// 1. Matching installers have NestedInstallerType: portable.
        /// 2. Matching installers have the same architecture.
        /// 3. Matching installers have the same hash.
        /// </summary>
        private static void MergeNestedInstallerFilesIfApplicable(InstallerManifest installerManifest)
        {
            var nestedPortableInstallers = installerManifest.Installers.Where(i => i.NestedInstallerType == NestedInstallerType.Portable).ToList();
            var mergeableInstallersList = nestedPortableInstallers.GroupBy(i => i.Architecture + i.InstallerSha256).ToList();
            foreach (var installers in mergeableInstallersList)
            {
                // First installer in each list is used to merge into
                var installerToMergeInto = installers.First();

                // Remove the first installer from the manifest, will be added back once the merge is complete
                installerManifest.Installers.Remove(installerToMergeInto);

                var installersToMerge = installers.Skip(1).ToList();

                // Append NestedInstallerFiles of other matching installers and remove them from the manifest
                foreach (var installer in installersToMerge)
                {
                    installerToMergeInto.NestedInstallerFiles.AddRange(installer.NestedInstallerFiles);
                    installerManifest.Installers.Remove(installer);
                }

                // Add the installer with the merged nested installer files back to the manifest
                installerManifest.Installers.Add(installerToMergeInto);
            }
        }

        private static void RemoveARPEntries(InstallerManifest installerManifest)
        {
            installerManifest.AppsAndFeaturesEntries = null;
            foreach (var installer in installerManifest.Installers)
            {
                installer.AppsAndFeaturesEntries = null;
            }
        }

        /// <summary>
        /// Prompts for the package identifier and applies the value to all manifests.
        /// </summary>
        /// <param name="manifests">Manifests object model.</param>
        private void PromptPackageIdentifier(Manifests manifests)
        {
            VersionManifest versionManifest = manifests.VersionManifest;
            PromptHelper.PromptPropertyAndSetValue(versionManifest, nameof(versionManifest.PackageIdentifier), versionManifest.PackageIdentifier);
            manifests.InstallerManifest.PackageIdentifier = versionManifest.PackageIdentifier;
            manifests.DefaultLocaleManifest.PackageIdentifier = versionManifest.PackageIdentifier;
        }

        /// <summary>
        /// Checks if the package identifier already exists in the default winget-pkgs repository.
        /// </summary>
        /// <param name="packageIdentifier">Package identifier string.</param>
        /// <returns>Boolean value indicating whether the package identifier is a duplicate and already exists.</returns>
        private async Task<bool> IsDuplicatePackageIdentifier(string packageIdentifier)
        {
            if (this.GitHubClient == null)
            {
                return false;
            }

            try
            {
                string exactMatch = await this.GitHubClient.FindPackageId(packageIdentifier);
                return !string.IsNullOrEmpty(exactMatch);
            }
            catch (Octokit.RateLimitExceededException)
            {
                Logger.ErrorLocalized(nameof(Resources.RateLimitExceeded_Message));
                return false;
            }
        }
    }
}
