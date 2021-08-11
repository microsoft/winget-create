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
        /// List of strings representing the optional fields that should not be editable.
        /// </summary>
        private static readonly string[] NonEditableOptionalFields = new[]
        {
            nameof(InstallerType),
            nameof(InstallerManifest.Channel),
            nameof(Installer.InstallerUrl),
            nameof(Installer.InstallerSha256),
            nameof(Installer.SignatureSha256),
            nameof(Installer.ProductCode),
            nameof(Installer.PackageFamilyName),
            nameof(Installer.AdditionalProperties),
        };

        /// <summary>
        /// List of strings re
        /// </summary>
        private static readonly string[] InstallerMenuDefaultItems = new[]
        {
            "ALL INSTALLERS",
            "DISPLAY PREVIEW",
            "NONE",
        };

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
                    PromptAndSetPropertyValue(this, nameof(this.InstallerUrls), validationModel: new Installer(), validationName: nameof(Installer.InstallerUrl));
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

                DisplayInstallersAsMenuSelection(manifests.InstallerManifest);

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

                    PromptAndSetPropertyValue(manifest, property.Name);
                    //Logger.Trace($"Property [{property.Name}] set to the value [{result}]");
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
                List<string> selectionList = new List<string>();

                foreach (Installer installer in installerManifest.Installers)
                {
                    var installerTuple = string.Join('|', installer.InstallerUrl, installer.InstallerType, installer.Architecture);
                    selectionList.Add(installerTuple);
                }

                selectionList.AddRange(InstallerMenuDefaultItems);
                var selectedItem = Prompt.Select("Which installer would you like to choose to edit?", selectionList);

                if (selectedItem == "NONE")
                {
                    break;
                }
                else if (selectedItem == "ALL INSTALLERS")
                {
                    Installer installerCopy = new Installer();
                    DisplayOptionalPropertiesAsMenuSelection(installerCopy);
                    ApplyChangesToIndividualInstallers(installerCopy, installerManifest.Installers);
                }
                else if (selectedItem == "DISPLAY PREVIEW")
                {
                    Console.Clear();
                    Console.WriteLine();
                    Console.WriteLine("Displaying a preview of the selected installer.");
                    var serializer = Serialization.CreateSerializer();
                    string installerString = serializer.Serialize(installerManifest);
                    Console.WriteLine(installerString);
                    Console.WriteLine();
                }
                else
                {
                    string[] selection = selectedItem.Split('|');
                    var selectedInstaller = installerManifest.Installers.Single(
                        item => item.InstallerUrl == selection[0] &&
                        item.InstallerType == (InstallerType)Enum.Parse(typeof(InstallerType), selection[1]) &&
                        item.Architecture == (InstallerArchitecture)Enum.Parse(typeof(InstallerArchitecture), selection[2]));

                    DisplayOptionalPropertiesAsMenuSelection(selectedInstaller);
                }
            }
        }

        private static void ApplyChangesToIndividualInstallers(Installer installerCopy, List<Installer> installers)
        {
            var modifiedFields = installerCopy.GetType().GetProperties().Select(prop => prop).Where(pi => pi.GetValue(installerCopy) != null);

            foreach (var field in modifiedFields)
            {
                foreach (Installer installer in installers)
                {
                    var fieldValue = field.GetValue(installerCopy);
                    installer.GetType().GetProperty(field.Name).SetValue(installer, fieldValue);
                }
            }
        }

        private static void DisplayOptionalPropertiesAsMenuSelection<T>(T model)
        {
            Console.Clear();

            var properties = model.GetType().GetProperties().ToList();
            var optionalProperties = properties.Where(p =>
                p.GetCustomAttribute<RequiredAttribute>() == null &&
                p.GetCustomAttribute<JsonPropertyAttribute>() != null).ToList();

            var fieldList = properties
                .Select(property => property.Name)
                .Where(pName => !NonEditableOptionalFields.Any(field => field == pName))
                .Append("NONE")
                .ToList();

            var installerTypeProperty = model.GetType().GetProperty(nameof(InstallerType));
            if (installerTypeProperty != null)
            {
                var installerType = installerTypeProperty.GetValue(model);
                if (installerType != null)
                {
                    if ((InstallerType)installerType == InstallerType.Msi)
                    {
                        fieldList.Add(nameof(Installer.ProductCode));
                    }
                    else if ((InstallerType)installerType == InstallerType.Msix)
                    {
                        fieldList.Add(nameof(Installer.PackageFamilyName));
                    }
                }
            }

            while (true)
            {
                Console.WriteLine("Which property would you like to edit?");
                var selectedField = Prompt.Select("Type for search results", fieldList);
                Console.Clear();

                if (selectedField == "NONE")
                {
                    break;
                }

                var selectedProperty = properties.First(p => p.Name == selectedField);

                PromptAndSetPropertyValue(model, selectedField);
            }
        }

        private static void PromptSubfieldProperties<T>(T field, PropertyInfo property, object model)
        {
            DisplayOptionalPropertiesAsMenuSelection(field);
            if (field.GetType().GetProperties()
                .Where(pi => !pi.PropertyType.IsDictionary())
                .Select(pi => pi.GetValue(field))
                .Any(value => value == null))
            {
                property.SetValue(model, field);
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
                PromptAndSetPropertyValue(manifest, property.Name);
                //Logger.Trace($"Property [{property.Name}] set to the value [{result}]");
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

                        PromptAndSetPropertyValue(installer, requiredProperty.Name);
                    }
                }
            }
        }

        private static void PromptInstallerSwitchesForExe<T>(T manifest)
        {
            InstallerSwitches installerSwitches = new InstallerSwitches();

            PromptAndSetPropertyValue(installerSwitches, nameof(InstallerSwitches.Silent));
            PromptAndSetPropertyValue(installerSwitches, nameof(InstallerSwitches.SilentWithProgress));

            if (!string.IsNullOrEmpty(installerSwitches.Silent) || !string.IsNullOrEmpty(installerSwitches.SilentWithProgress))
            {
                manifest.GetType().GetProperty(nameof(InstallerSwitches)).SetValue(manifest, installerSwitches);
            }
        }

        private static void PromptAndSetPropertyValue(object model, string memberName, object validationModel = null, string validationName = null, string message = null)
        {
            if (string.IsNullOrEmpty(validationName))
            {
                validationName = memberName;
            }

            if (validationModel == null)
            {
                validationModel = model;
            }

            var property = model.GetType().GetProperty(memberName);
            var defaultValue = property.GetValue(model);
            Type propertyType = property.PropertyType;
            Type elementType;
            message = $"[{memberName}] value is";
            Console.WriteLine(Resources.ResourceManager.GetString($"{memberName}_KeywordDescription"));

            if (propertyType == typeof(string))
            {
                var result = Prompt.Input<string>(message, property.GetValue(model), new[] { FieldValidation.ValidateProperty(validationModel, validationName, defaultValue) });
                property.SetValue(model, result);
            }
            else if (propertyType.IsEnum)
            {
                PromptEnum(model, property, defaultValue.GetType(), true, message, defaultValue.ToString());
            }
            else if ((elementType = Nullable.GetUnderlyingType(propertyType)) != null)
            {
                if (elementType.IsEnum)
                {
                    PromptEnum(model, property, elementType, false, message: message);
                }
            }
            else if (propertyType.IsList())
            {
                elementType = propertyType.GetGenericArguments().SingleOrDefault();
                if (elementType == typeof(string) || typeof(IEnumerable<string>).IsAssignableFrom(propertyType))
                {
                    var value = Prompt.List<string>(message, minimum: 0, validators: new[] { FieldValidation.ValidateProperty(validationModel, validationName, defaultValue) });
                    property.SetValue(model, value);
                }
                else if (elementType == typeof(int))
                {
                    // The only field that takes in List<int> is InstallerSuccessCodes, which only has constraints on the number of entries.
                    // TODO: Add validation for checking the number of entries in the list.
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
                            var enumItem = Enum.Parse(elementType, item);
                            enumList.Add(enumItem);
                        }

                        property.SetValue(model, enumList);
                    }
                }
                else if (elementType == typeof(PackageDependencies))
                {
                    List<PackageDependencies> packageDependencies = (List<PackageDependencies>)property.GetValue(model) ?? new List<PackageDependencies>();

                    PromptForPackageDependencies(packageDependencies);
                    if (packageDependencies.Any())
                    {
                        property.SetValue(model, packageDependencies);
                    }
                    else
                    {
                        property.SetValue(model, null);
                    }
                }
            }
            else
            {
                if (property.PropertyType == typeof(InstallerSwitches))
                {
                    InstallerSwitches installerSwitches = (InstallerSwitches)property.GetValue(model) ?? new InstallerSwitches();
                    PromptSubfieldProperties(installerSwitches, property, model);
                }
                else if (property.PropertyType == typeof(Dependencies))
                {
                    Dependencies dependencies = (Dependencies)property.GetValue(model) ?? new Dependencies();
                    PromptSubfieldProperties(dependencies, property, model);
                }
            }
        }

        private static void PromptForPackageDependencies(List<PackageDependencies> packageDependencies)
        {

            var serializer = Serialization.CreateSerializer();


            string selection = string.Empty;
            while (selection != "NONE")
            {
                Console.Clear();
                if (packageDependencies.Any())
                {
                    Logger.Info($"Showing preview of [{nameof(PackageDependencies)}]");
                    string serializedString = serializer.Serialize(packageDependencies);
                    Console.WriteLine(serializedString);
                    Console.WriteLine();
                }

                selection = Prompt.Select("What would you like to do?", new[] { "ADD", "REMOVE LAST ENTRY", "NONE" });
                if (selection == "ADD")
                {
                    PackageDependencies newDependency = new PackageDependencies();
                    DisplayOptionalPropertiesAsMenuSelection(newDependency);
                    if (!string.IsNullOrEmpty(newDependency.PackageIdentifier) && !string.IsNullOrEmpty(newDependency.MinimumVersion))
                    {
                        packageDependencies.Add(newDependency);
                    }
                }
                else if (selection == "REMOVE LAST ENTRY")
                {
                    if (packageDependencies.Any())
                    {
                        packageDependencies.RemoveAt(packageDependencies.Count - 1);
                    }
                }
            }
        }

        private static void PromptEnum(object model, PropertyInfo property, Type enumType, bool required, string message, string defaultValue = null)
        {
            var enumList = Enum.GetNames(enumType);

            if (!required)
            {
                enumList.Append("NONE");
            }

            var value = Prompt.Select(message, enumList, defaultValue: defaultValue);
            if (value != "NONE")
            {
                property.SetValue(model, Enum.Parse(enumType, value));
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
            PromptAndSetPropertyValue(versionManifest, nameof(versionManifest.PackageIdentifier));

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
