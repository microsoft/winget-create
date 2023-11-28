// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    using Microsoft.WingetCreateCore.Models.Locale;
    using Sharprompt;

    /// <summary>
    /// Command to create a new locale manifest for an existing manifest in the Windows Package Manager repository.
    /// </summary>
    [Verb("new-locale", HelpText = "NewLocaleCommand_HelpText", ResourceType = typeof(Resources))]
    public class NewLocaleCommand : BaseCommand
    {
        /// <summary>
        /// Gets the usage examples for the new-locale command.
        /// </summary>
        [Usage(ApplicationAlias = ProgramApplicationAlias)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example(Resources.Example_NewLocaleCommand_AddForLatestVersion, new NewLocaleCommand { Id = "<PackageIdentifier>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_NewLocaleCommand_AddForSpecificVersion, new NewLocaleCommand { Id = "<PackageIdentifier>", Version = "<Version>", GitHubToken = "<GitHubPersonalAccessToken>" });
                yield return new Example(Resources.Example_NewLocaleCommand_SaveToDirectory, new NewLocaleCommand { Id = "<PackageIdentifier>", Locale = "<Locale>", Version = "<Version>", OutputDir = "<OutputDirectory>", GitHubToken = "<GitHubPersonalAccessToken>" });
            }
        }

        /// <summary>
        /// Gets or sets the id used for looking up an existing manifest in the Windows Package Manager repository.
        /// </summary>
        [Value(0, MetaName = "PackageIdentifier", Required = true, HelpText = "PackageIdentifier_HelpText", ResourceType = typeof(Resources))]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the version of the package from the Windows Package Manager repository to create the new locales for.
        /// </summary>
        [Option('v', "version", Required = false, Default = null, HelpText = "NewLocaleCommand_Version_HelpText", ResourceType = typeof(Resources))]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the locale to create a new manifest for.
        /// </summary>
        [Option('l', "locale", Required = false, HelpText = "NewLocaleCommand_Locale_HelpText", ResourceType = typeof(Resources))]
        public string Locale { get; set; }

        /// <summary>
        /// Gets or sets the reference locale to use for generating the new locale manifest.
        /// </summary>
        [Option('r', "reference-locale", Required = false, HelpText = "ReferenceLocale_HelpText", ResourceType = typeof(Resources))]
        public string ReferenceLocale { get; set; }

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
        /// Executes the new-locale command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override async Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(NewLocaleCommand),
                Id = this.Id,
                Version = this.Version,
                HasGitHubToken = !string.IsNullOrEmpty(this.GitHubToken),
            };

            Prompt.Symbols.Done = new Symbol(string.Empty, string.Empty);
            Prompt.Symbols.Prompt = new Symbol(string.Empty, string.Empty);

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

            List<string> manifestContent;

            try
            {
                manifestContent = await this.GitHubClient.GetManifestContentAsync(this.Id, this.Version);
                Manifests originalManifests = Serialization.DeserializeManifestContents(manifestContent);

                // Validate input locale argument and switch command flow if applicable.
                if (!string.IsNullOrEmpty(this.Locale))
                {
                    try
                    {
                        if (LocaleHelper.GetMatchingLocaleManifest(this.Locale, originalManifests) != null)
                        {
                            Logger.ErrorLocalized(nameof(Resources.LocaleAlreadyExists_ErrorMessage), this.Locale);
                            Console.WriteLine();

                            // Switch to update locale flow if user accepts to.
                            if (Prompt.Confirm(Resources.SwitchToUpdateLocaleFlow_Message))
                            {
                                UpdateLocaleCommand command = new UpdateLocaleCommand
                                {
                                    Id = this.Id,
                                    Version = this.Version,
                                    Locale = this.Locale,
                                    GitHubToken = this.GitHubToken,
                                };
                                await command.LoadGitHubClient();
                                Console.WriteLine();
                                return await command.Execute();
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                        return false;
                    }
                }

                Console.WriteLine(Resources.NewLocaleCommand_Header);
                Console.WriteLine();
                Logger.InfoLocalized(nameof(Resources.ManifestDocumentation_HelpText), Constants.ManifestDocumentationUrl);
                Console.WriteLine();
                Console.WriteLine(Resources.NewLocaleCommand_PrePrompt_Header);
                Console.WriteLine();

                Logger.DebugLocalized(nameof(Resources.EnterFollowingFields_Message));

                List<LocaleManifest> newLocales = this.ParseArgumentsAndGenerateLocales(originalManifests);

                if (newLocales == null)
                {
                    return false;
                }

                Console.WriteLine();
                EnsureManifestVersionConsistency(originalManifests);
                DisplayGeneratedLocales(newLocales);

                if (string.IsNullOrEmpty(this.OutputDir))
                {
                    this.OutputDir = Directory.GetCurrentDirectory();
                }

                string manifestDirectoryPath = SaveManifestDirToLocalPath(originalManifests, this.OutputDir);

                if (ValidateManifest(manifestDirectoryPath))
                {
                    if (Prompt.Confirm(Resources.ConfirmGitHubSubmitManifest_Message))
                    {
                        return await this.LoadGitHubClient(true) ?
                            (commandEvent.IsSuccessful = await this.GitHubSubmitManifests(
                                originalManifests,
                                this.GetPRTitle(originalManifests, null, nameof(NewLocaleCommand))))
                            : false;
                    }
                    else
                    {
                        Logger.WarnLocalized(nameof(Resources.SkippingPullRequest_Message));
                    }
                }
                else
                {
                    return false;
                }

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

        private static void DisplayGeneratedLocales(List<LocaleManifest> newLocales)
        {
            Logger.DebugLocalized(nameof(Resources.GenerateNewLocalePreview_Message));
            foreach (var localeManifest in newLocales)
            {
                Logger.InfoLocalized(nameof(Resources.LocaleManifest_Message), localeManifest.PackageLocale);
                Console.WriteLine(localeManifest.ToYaml(true));
            }
        }

        private static void FillLocalePropertiesForUserCompletion(LocaleManifest fromManifest, LocaleManifest toManifest, List<string> properties)
        {
            foreach (string propertyName in properties)
            {
                if (propertyName == nameof(LocaleManifest.PackageLocale))
                {
                    continue;
                }

                var value = fromManifest.GetType().GetProperty(propertyName).GetValue(fromManifest);
                if (value != null)
                {
                    toManifest.GetType().GetProperty(propertyName).SetValue(toManifest, value);
                }
            }
        }

        private List<LocaleManifest> ParseArgumentsAndGenerateLocales(Manifests originalManifests)
        {
            LocaleManifest referenceLocaleManifest;

            if (!string.IsNullOrEmpty(this.ReferenceLocale))
            {
                try
                {
                    referenceLocaleManifest = (LocaleManifest)LocaleHelper.GetMatchingLocaleManifest(this.ReferenceLocale, originalManifests);
                }
                catch (ArgumentException)
                {
                    Logger.ErrorLocalized(nameof(Resources.InvalidLocale_ErrorMessage));
                    return null;
                }

                if (referenceLocaleManifest == null)
                {
                    Logger.ErrorLocalized(nameof(Resources.ReferenceLocaleNotFound_Error));
                    return null;
                }
            }
            else
            {
                referenceLocaleManifest = originalManifests.DefaultLocaleManifest.ToLocaleManifest();
            }

            List<LocaleManifest> generatedLocales = new();

            List<string> defaultPromptPropertiesForNewLocale = new()
            {
                nameof(LocaleManifest.PackageLocale),
                nameof(LocaleManifest.PackageName),
                nameof(LocaleManifest.Publisher),
                nameof(LocaleManifest.License),
                nameof(LocaleManifest.ShortDescription),
            };

            bool localeArgumentUsed = false;
            do
            {
                LocaleManifest newLocaleManifest = new LocaleManifest
                {
                    PackageIdentifier = originalManifests.VersionManifest.PackageIdentifier,
                    PackageVersion = originalManifests.VersionManifest.PackageVersion,
                };

                // Fill in properties from the reference locale. This is to help user see previous values to quickly fill out the new manifest.
                FillLocalePropertiesForUserCompletion(referenceLocaleManifest, newLocaleManifest, defaultPromptPropertiesForNewLocale);

                if (!string.IsNullOrEmpty(this.Locale) && !localeArgumentUsed)
                {
                    newLocaleManifest.PackageLocale = this.Locale;

                    // Don't prompt for PackageLocale if it is provided as an argument.
                    List<string> properties = new(defaultPromptPropertiesForNewLocale);
                    properties.Remove(nameof(LocaleManifest.PackageLocale));

                    Logger.DebugLocalized(nameof(Resources.FieldSetToValue_Message), nameof(LocaleManifest.PackageLocale), this.Locale);
                    LocaleHelper.PromptAndSetLocaleProperties(newLocaleManifest, properties, originalManifests);
                    localeArgumentUsed = true;
                }
                else
                {
                    LocaleHelper.PromptAndSetLocaleProperties(newLocaleManifest, defaultPromptPropertiesForNewLocale, originalManifests);
                }

                Console.WriteLine();
                if (Prompt.Confirm(Resources.AddAdditionalLocaleProperties_Message))
                {
                    // Get optional properties that have not been prompted before.
                    var optionalProperties = LocaleHelper.GetUnPromptedLocalePropertyNames(newLocaleManifest, defaultPromptPropertiesForNewLocale);
                    FillLocalePropertiesForUserCompletion(referenceLocaleManifest, newLocaleManifest, optionalProperties);
                    PromptOptionalProperties(newLocaleManifest, optionalProperties);
                }

                originalManifests.LocaleManifests.Add(newLocaleManifest);
                generatedLocales.Add(newLocaleManifest);

                Console.WriteLine();
                ValidateManifestsInTempDir(originalManifests);
            }
            while (Prompt.Confirm(Resources.CreateAnotherLocale_Message));

            return generatedLocales;
        }
    }
}
