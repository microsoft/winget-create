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
        public string GitHubToken { get; set; }

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
                    this.InstallerUrls = PromptProperty(
                        new Installer(),
                        this.InstallerUrls,
                        nameof(Installer.InstallerUrl));
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
                    if (await this.SetAndCheckGitHubToken(this.GitHubToken))
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
            if (Prompt.Confirm(Resources.ModifyOptionalFields_Message))
            {
                PromptOptionalProperties(manifests.DefaultLocaleManifest);
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

                    var currentValue = property.GetValue(manifest);
                    var result = PromptProperty(manifest, currentValue, property.Name);
                    property.SetValue(manifest, result);
                    Logger.Trace($"Property [{property.Name}] set to the value [{result}]");
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
                var currentValue = property.GetValue(manifest);
                var result = PromptProperty(manifest, currentValue, property.Name);
                property.SetValue(manifest, result);
                Logger.Trace($"Property [{property.Name}] set to the value [{result}]");
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
                    Logger.Debug($"Additional metadata needed for installer from {installer.InstallerUrl}");
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
                            Logger.Debug($"Additional metadata needed for installer from {installer.InstallerUrl}");
                            prompted = true;
                        }

                        var result = PromptProperty(installer, currentValue, requiredProperty.Name);
                        requiredProperty.SetValue(installer, result);
                    }
                }
            }
        }

        private static void PromptInstallerSwitchesForExe<T>(T manifest)
        {
            InstallerSwitches installerSwitches = new InstallerSwitches();

            var silentSwitchResult = PromptProperty(installerSwitches, installerSwitches.Silent, nameof(InstallerSwitches.Silent));
            var silentWithProgressSwitchResult = PromptProperty(installerSwitches, installerSwitches.SilentWithProgress, nameof(InstallerSwitches.SilentWithProgress));

            bool updateSwitches = false;

            if (!string.IsNullOrEmpty(silentSwitchResult))
            {
                installerSwitches.Silent = silentSwitchResult;
                updateSwitches = true;
            }

            if (!string.IsNullOrEmpty(silentWithProgressSwitchResult))
            {
                installerSwitches.SilentWithProgress = silentWithProgressSwitchResult;
                updateSwitches = true;
            }

            if (updateSwitches)
            {
                manifest.GetType().GetProperty(nameof(InstallerSwitches)).SetValue(manifest, installerSwitches);
            }
        }

        private static T PromptProperty<T>(object model, T property, string memberName, string message = null)
        {
            message ??= $"[{memberName}] " +
            Resources.ResourceManager.GetString($"{memberName}_KeywordDescription") ?? memberName;

            // Because some properties don't have a current value, we can't rely on T or the property to obtain the type.
            // Use reflection to obtain the type by looking up the property type by membername based on the model.
            Type typeFromModel = model.GetType().GetProperty(memberName).PropertyType;
            if (typeFromModel.IsEnum)
            {
                // For enums, we want to call Prompt.Select<T>, specifically the overload that takes 4 parameters
                var generic = typeof(Prompt)
                    .GetMethods()
                    .Where(mi => mi.Name == nameof(Prompt.Select) && mi.GetParameters().Length == 5)
                    .Single()
                    .MakeGenericMethod(property.GetType());

                return (T)generic.Invoke(null, new object[] { message, property.GetType().GetEnumValues(), null, property, null });
            }
            else if (typeof(IEnumerable<string>).IsAssignableFrom(typeof(T)) || typeof(IEnumerable<string>).IsAssignableFrom(typeFromModel))
            {
                string combinedString = null;

                if (property is IEnumerable<string> propList && propList.Any())
                {
                    combinedString = string.Join(", ", propList);
                }

                // Take in a comma-delimited string, and validate each split item, then return the split array
                string promptResult = Prompt.Input<string>(message, combinedString, new[] { FieldValidation.ValidateProperty(model, memberName, property) });
                return (T)(object)promptResult?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else
            {
                var promptResult = Prompt.Input<T>(message, property, new[] { FieldValidation.ValidateProperty(model, memberName, property) });
                return promptResult is string str ? (T)(object)str.Trim() : promptResult;
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
                if (!await this.SetAndCheckGitHubToken(this.GitHubToken))
                {
                    return false;
                }
            }

            VersionManifest versionManifest = manifests.VersionManifest;
            versionManifest.PackageIdentifier = PromptProperty(versionManifest, versionManifest.PackageIdentifier, nameof(versionManifest.PackageIdentifier));

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
