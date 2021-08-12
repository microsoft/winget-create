// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
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
    using Microsoft.WingetCreateCore.Models.Version;
    using Newtonsoft.Json;
    using Sharprompt;

    /// <summary>
    /// Command to launch a wizard that prompt users for information to generate a new manifest.
    /// </summary>
    [Verb("new", HelpText = "NewCommand_HelpText", ResourceType = typeof(Resources))]
    public class NewCommand : BaseCommand
    {
        /// <summary>
        /// The url path to the manifest documentation site.
        /// </summary>
        private const string ManifestDocumentationUrl = "https://github.com/microsoft/winget-cli/blob/master/doc/ManifestSpecv1.0.md";

        /// <summary>
        /// Installer types for which we can trust that the detected architecture is correct, so don't need to prompt the user to confirm.
        /// </summary>
        private static readonly InstallerType[] ReliableArchitectureInstallerTypes = new[] { InstallerType.Msix, InstallerType.Appx };

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
                    InstallerUrls = new string[] { "<InstallerUrl1>", "<InstallerUrl2>, .." },
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
        /// Gets or sets the GitHub token used to submit a pull request on behalf of the user.
        /// </summary>
        [Option('t', "token", Required = false, HelpText = "GitHubToken_HelpText", ResourceType = typeof(Resources))]
        public override string GitHubToken { get => base.GitHubToken; set => base.GitHubToken = value; }

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
                    PromptHelper.PromptAndSetPropertyValue(this, nameof(this.InstallerUrls), minimum: 1, validationModel: new Installer(), validationName: nameof(Installer.InstallerUrl));
                    Console.Clear();
                }

                var packageFiles = await DownloadInstallers(this.InstallerUrls);
                if (packageFiles == null)
                {
                    return false;
                }

                if (!PackageParser.ParsePackages(
                    packageFiles,
                    this.InstallerUrls,
                    manifests,
                    out List<PackageParser.DetectedArch> detectedArchs))
                {
                    Logger.ErrorLocalized(nameof(Resources.PackageParsing_Error));
                    return false;
                }

                DisplayMismatchedArchitectures(detectedArchs);

                Console.WriteLine(Resources.NewCommand_Header);
                Console.WriteLine();
                Logger.InfoLocalized(nameof(Resources.ManifestDocumentation_HelpText), ManifestDocumentationUrl);
                Console.WriteLine();
                Console.WriteLine(Resources.NewCommand_Description);
                Console.WriteLine();

                Logger.DebugLocalized(nameof(Resources.EnterFollowingFields_Message));

                bool isManifestValid;

                do
                {
                    if (this.WingetRepoOwner == DefaultWingetRepoOwner &&
                        this.WingetRepo == DefaultWingetRepo &&
                        !await this.PromptPackageIdentifierAndCheckDuplicates(manifests))
                    {
                        Console.WriteLine();
                        Logger.ErrorLocalized(nameof(Resources.PackageIdAlreadyExists_Error));
                        return false;
                    }

                    PromptPropertiesAndDisplayManifests(manifests);
                    isManifestValid = ValidateManifestsInTempDir(manifests);
                }
                while (Prompt.Confirm(Resources.ConfirmManifestCreation_Message));

                if (string.IsNullOrEmpty(this.OutputDir))
                {
                    this.OutputDir = Directory.GetCurrentDirectory();
                }

                SaveManifestDirToLocalPath(manifests, this.OutputDir);

                if (isManifestValid && Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message))
                {
                    if (await this.SetAndCheckGitHubToken())
                    {
                        return commandEvent.IsSuccessful = await this.GitHubSubmitManifests(manifests, this.GitHubToken);
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

        private static void PromptPropertiesAndDisplayManifests(Manifests manifests)
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
                DisplayInstallersAsMenuSelection(manifests.InstallerManifest);
            }

            Console.WriteLine();
            DisplayManifestPreview(manifests);
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

                    PromptHelper.PromptAndSetPropertyValue(manifest, property.Name);
                    Logger.Trace($"Property [{property.Name}] set to the value [{property.GetValue(manifest)}]");
                }
            }
        }

        /// <summary>
        /// Displays all installers from an Installer manifest as a selection menu.
        /// </summary>
        private static void DisplayInstallersAsMenuSelection(InstallerManifest installerManifest)
        {
            Console.Clear();

            while (true)
            {
                List<string> selectionList = GenerateInstallerSelectionList(installerManifest.Installers);
                var selectedItem = Prompt.Select(Resources.SelectInstallerToEdit_Message, selectionList);

                if (selectedItem == Resources.None_MenuItem)
                {
                    break;
                }
                else if (selectedItem == Resources.AllInstallers_MenuItem)
                {
                    Installer installerCopy = new Installer();
                    PromptHelper.DisplayPropertiesAsMenuSelection(installerCopy, Resources.None_MenuItem);
                    ApplyChangesToIndividualInstallers(installerCopy, installerManifest.Installers);
                }
                else if (selectedItem == Resources.DisplayPreview_MenuItem)
                {
                    Console.Clear();
                    Console.WriteLine();
                    Logger.InfoLocalized(nameof(Resources.DisplayPreviewOfSelectedInstaller_Message));
                    var serializer = Serialization.CreateSerializer();
                    string installerString = serializer.Serialize(installerManifest);
                    Console.WriteLine(installerString);
                    Console.WriteLine();
                }
                else
                {
                    Installer selectedInstaller = MatchMenuSelectionToInstaller(selectedItem, installerManifest);
                    PromptHelper.DisplayPropertiesAsMenuSelection(selectedInstaller, Resources.None_MenuItem);
                }
            }
        }

        private static Installer MatchMenuSelectionToInstaller(string selectedItem, InstallerManifest installerManifest)
        {
            string[] selection = selectedItem.Split('|');
            var matchedInstallers = installerManifest.Installers.Where(
                item => item.Architecture == (InstallerArchitecture)Enum.Parse(typeof(InstallerArchitecture), selection[0]) &&
                item.InstallerType == (InstallerType)Enum.Parse(typeof(InstallerType), selection[1], true) &&
                item.InstallerUrl == selection[2]);

            Installer selectedInstaller;
            if (matchedInstallers.Count() > 1)
            {
                Scope scope;
                Scope? nullableScope = null;
                string installerLocale = string.Empty;

                for (int i = 3; i < selection.Length; i++)
                {
                    // if parsing for scope enum fails, value must be an installer locale
                    if (Enum.TryParse(selection[i], true, out scope))
                    {
                        nullableScope = scope;
                    }
                    else
                    {
                        installerLocale = selection[i];
                    }
                }

                selectedInstaller = matchedInstallers.Single(
                    item => nullableScope != null && item.Scope == nullableScope &&
                    !string.IsNullOrEmpty(installerLocale) && item.InstallerLocale == installerLocale);
            }
            else
            {
                selectedInstaller = matchedInstallers.Single();
            }

            return selectedInstaller;
        }

        private static List<string> GenerateInstallerSelectionList(List<Installer> installers)
        {
            List<string> selectionList = new List<string>();
            selectionList.Add(Resources.AllInstallers_MenuItem);

            foreach (Installer installer in installers)
            {
                var installerTuple = string.Join('|', installer.Architecture, installer.InstallerType.ToEnumAttributeValue(), installer.InstallerUrl);
                if (installer.Scope != null)
                {
                    installerTuple = string.Join('|', installerTuple, installer.Scope);
                }

                if (installer.InstallerLocale != null)
                {
                    installerTuple = string.Join('|', installerTuple, installer.InstallerLocale);
                }

                selectionList.Add(installerTuple);
            }

            selectionList.Add(Resources.DisplayPreview_MenuItem);
            selectionList.Add(Resources.None_MenuItem);
            return selectionList;
        }

        private static void ApplyChangesToIndividualInstallers(Installer installerCopy, List<Installer> installers)
        {
            var modifiedFields = installerCopy.GetType().GetProperties()
                .Select(prop => prop)
                .Where(pi => pi.GetValue(installerCopy) != null && pi.Name != nameof(Installer.Architecture));

            foreach (var field in modifiedFields)
            {
                foreach (Installer installer in installers)
                {
                    var fieldValue = field.GetValue(installerCopy);
                    installer.GetType().GetProperty(field.Name).SetValue(installer, fieldValue);
                }
            }
        }

        private static void PromptOptionalProperties<T>(T manifest)
        {
            var properties = manifest.GetType().GetProperties().ToList();
            var optionalProperties = properties.Where(p =>
                p.GetCustomAttribute<RequiredAttribute>() == null &&
                p.GetCustomAttribute<JsonPropertyAttribute>() != null).ToList();

            foreach (var property in optionalProperties)
            {
                PromptHelper.PromptAndSetPropertyValue(manifest, property.Name);
                Logger.Trace($"Property [{property.Name}] set to the value [{property.GetValue(manifest)}]");
            }
        }

        private static void PromptInstallerProperties<T>(T manifest, PropertyInfo property)
        {
            List<Installer> installers = new List<Installer>((ICollection<Installer>)property.GetValue(manifest));
            foreach (var installer in installers)
            {
                var installerProperties = installer.GetType().GetProperties().ToList();

                var requiredInstallerProperties = installerProperties
                    .Where(p => Attribute.IsDefined(p, typeof(RequiredAttribute)) || p.Name == nameof(InstallerType)).ToList();

                bool prompted = false;

                // If we know the installertype is EXE, prompt the user for installer switches (silent and silentwithprogress)
                if (installer.InstallerType == InstallerType.Exe)
                {
                    Console.WriteLine();
                    Logger.DebugLocalized(nameof(Resources.AdditionalMetadataNeeded_Message), installer.InstallerUrl);
                    prompted = true;
                    PromptInstallerSwitchesForExe(manifest);
                }

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

                        PromptHelper.PromptAndSetPropertyValue(installer, requiredProperty.Name);
                    }
                }
            }
        }

        private static void PromptInstallerSwitchesForExe<T>(T manifest)
        {
            InstallerSwitches installerSwitches = new InstallerSwitches();

            PromptHelper.PromptAndSetPropertyValue(installerSwitches, nameof(InstallerSwitches.Silent));
            PromptHelper.PromptAndSetPropertyValue(installerSwitches, nameof(InstallerSwitches.SilentWithProgress));

            if (!string.IsNullOrEmpty(installerSwitches.Silent) || !string.IsNullOrEmpty(installerSwitches.SilentWithProgress))
            {
                manifest.GetType().GetProperty(nameof(InstallerSwitches)).SetValue(manifest, installerSwitches);
            }
        }

        /// <summary>
        /// Prompts for the package identifier and checks if the package identifier already exists.
        /// If the package identifier is valid, the value is applied to the other manifests.
        /// </summary>
        /// <param name="manifests">Manifests object model.</param>
        /// <returns>Boolean value indicating whether the package identifier is valid.</returns>
        private async Task<bool> PromptPackageIdentifierAndCheckDuplicates(Manifests manifests)
        {
            GitHub client = new GitHub(this.GitHubToken, this.WingetRepoOwner, this.WingetRepo);

            if (!string.IsNullOrEmpty(this.GitHubToken))
            {
                if (!await this.SetAndCheckGitHubToken())
                {
                    return false;
                }
            }

            VersionManifest versionManifest = manifests.VersionManifest;
            PromptHelper.PromptAndSetPropertyValue(versionManifest, nameof(versionManifest.PackageIdentifier));

            string exactMatch = await client.FindPackageId(versionManifest.PackageIdentifier);

            if (!string.IsNullOrEmpty(exactMatch))
            {
                return false;
            }
            else
            {
                manifests.InstallerManifest.PackageIdentifier = versionManifest.PackageIdentifier;
                manifests.DefaultLocaleManifest.PackageIdentifier = versionManifest.PackageIdentifier;
                return true;
            }
        }
    }
}
