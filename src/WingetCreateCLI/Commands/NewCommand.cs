// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections;
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

                // test optional installer manifests here
                DisplayInstallersAsMenuSelection(manifests.InstallerManifest);
                // test optional installer manifests here end

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

        /// <summary>
        /// Displays all installers from an Installer manifest as a selection menu.
        /// </summary>
        private static void DisplayInstallersAsMenuSelection(InstallerManifest installerManifest)
        {
            // foreach installerManifest show the installerurl, architecture, and installertype, which will be used for matching
            List<string> tupleList = new List<string>();

            foreach (Installer installer in installerManifest.Installers)
            {
                var installerTuple = string.Join('|', installer.InstallerUrl, installer.InstallerType, installer.Architecture);
                tupleList.Add(installerTuple);
            }

            tupleList.Add("ALL INSTALLERS");
            tupleList.Add("NONE");

            while (true)
            {
                var selectedItem = Prompt.Select("Which installer would you like to choose?", tupleList);

                if (selectedItem == "NONE")
                {
                    break;
                }
                else if (selectedItem == "ALL INSTALLERS")
                {
                    DisplayOptionalPropertiesAsMenuSelection(installerManifest);
                }
                else
                {
                    string[] selection = selectedItem.Split('|');
                    var selectedInstaller = installerManifest.Installers.Single(
                        item => item.InstallerUrl == selection[0] &&
                        item.InstallerType == (InstallerType)Enum.Parse(typeof(InstallerType), selection[1]) &&
                        item.Architecture == (InstallerArchitecture)Enum.Parse(typeof(InstallerArchitecture), selection[2]));

                    Console.WriteLine();
                    Console.WriteLine("Displaying a preview of the selected installer.");
                    var serializer = Serialization.CreateSerializer();
                    string installerString = serializer.Serialize(selectedInstaller);
                    Console.WriteLine(installerString);
                    DisplayOptionalPropertiesAsMenuSelection(selectedInstaller);
                }
            }
        }

        private static void DisplayOptionalPropertiesAsMenuSelection<T>(T model)
        {
            var properties = model.GetType().GetProperties().ToList();
            var optionalProperties = properties.Where(p =>
                p.GetCustomAttribute<RequiredAttribute>() == null &&
                p.GetCustomAttribute<JsonPropertyAttribute>() != null).ToList();

            var fieldList = properties.Select(property => property.Name)
                .Where(p => p != "AdditionalProperties").ToList()
                .Append("NONE");

            while (true)
            {
                var selectedField = Prompt.Select("Which property would you like to edit?", fieldList);
                if (selectedField == "NONE")
                {
                    break;
                }
                var selectedProperty = properties.First(p => p.Name == selectedField);
                PromptOptionalInstallerProperty(model, selectedField);
            }
        }

        private static void PromptOptionalInstallerProperty(object model, string memberName)
        {
            // given the model and name, we should be able to obtain the property and its current value as well as the message string.
            var property = model.GetType().GetProperty(memberName);
            string message = $"[{memberName}] " + Resources.ResourceManager.GetString($"{memberName}_KeywordDescription") ?? memberName;
            Type elementType;

            if (property.PropertyType == typeof(string) || property.PropertyType.IsEnum)
            {
                // Handles string type or enum type fields
                var value = PromptProperty(model, model.GetType().GetProperty(memberName).GetValue(model), memberName);
                property.SetValue(model, value);
            }
            else if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                // Handles fields that take in a list of values
                elementType = property.PropertyType.GetGenericArguments()[0];

                if (elementType == typeof(string))
                {
                    // the issue with the list is that you can't delete existing values or modify them
                    ListOptions<string> listOptions = new ListOptions<string>() { Message = message, Minimum = 0 };
                    var value = Prompt.List<string>(listOptions);
                    property.SetValue(model, value);
                }
                else if (elementType == typeof(int))
                {
                    var value = Prompt.List<int>(message, minimum: 0);
                    property.SetValue(model, value);
                }
                else if (elementType.IsEnum)
                {
                    var value = Prompt.MultiSelect(message, Enum.GetNames(elementType), minimum: 0);

                    if (value.Any())
                    {
                        Type genericListType = typeof(List<>).MakeGenericType(elementType);
                        var enumList = (IList)Activator.CreateInstance(genericListType);

                        foreach (var item in value)
                        {
                            dynamic enumItem = Enum.Parse(elementType, item);
                            enumList.Add(enumItem);
                        }

                        property.SetValue(model, enumList);
                    }
                }
            }
            else if ((elementType = Nullable.GetUnderlyingType(property.PropertyType)) != null)
            {
                // Handles nullable types (Enum?)
                if (elementType.IsEnum)
                {
                    var value = Prompt.Select(message, Enum.GetNames(elementType).Append("NONE"));
                    if (value != "NONE")
                    {
                        property.SetValue(model, Enum.Parse(elementType, value));
                    }
                }
            }
            else if (property.PropertyType == typeof(InstallerSwitches))
            {
                // Handles installer switches
                InstallerSwitches installerSwitches = (InstallerSwitches)property.GetValue(model) ?? new InstallerSwitches();
                DisplayOptionalPropertiesAsMenuSelection(installerSwitches);
            }
            else if (property.PropertyType == typeof(Dependencies))
            {
                // Handles dependencies
                Dependencies dependencies = (Dependencies)property.GetValue(model) ?? new Dependencies();
                DisplayOptionalPropertiesAsMenuSelection(dependencies);
            }
            else if (property.PropertyType == typeof(PackageDependencies))
            {
                PackageDependencies packageDependencies = (PackageDependencies)property.GetValue(model) ?? new PackageDependencies();
                DisplayOptionalPropertiesAsMenuSelection(packageDependencies);
            }

            Console.WriteLine();
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
            // WARNING: this will throw a null exception if the property is not already instantiated.
            //var currentValue = model.GetType().GetProperty(memberName);

            message ??= $"[{memberName}] " +
            Resources.ResourceManager.GetString($"{memberName}_KeywordDescription") ?? memberName;

            // Because some properties don't have a current value, we can't rely on T or the property to obtain the type.
            // Use reflection to obtain the type by looking up the property type by membername based on the model.
            Type typeFromModel = model.GetType().GetProperty(memberName).PropertyType;
            if (typeFromModel.IsEnum)
            {
                // For enums, we want to call Prompt.Select<T>, specifically the overload that takes 5 parameters
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
                if (!await this.SetAndCheckGitHubToken())
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
