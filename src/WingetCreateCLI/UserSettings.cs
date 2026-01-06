// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// UserSettings configuration class for WingetCreate.
    /// </summary>
    public static class UserSettings
    {
        private static readonly Lazy<string> SettingsPathLazy = new(() => Path.Combine(Common.LocalAppStatePath, "settings.json"));

        private static readonly Lazy<string> SettingsBackupPathLazy = new(() => Path.Combine(Common.LocalAppStatePath, "settings.json.backup"));

        static UserSettings()
        {
            LoadSettings();
        }

        /// <summary>
        /// Gets the path to the settings json file.
        /// </summary>
        public static string SettingsJsonPath => SettingsPathLazy.Value;

        /// <summary>
        /// Gets the path to the backup settings json file.
        /// </summary>
        public static string SettingsBackupJsonPath => SettingsBackupPathLazy.Value;

        /// <summary>
        /// Gets or sets a value indicating whether to disable telemetry.
        /// </summary>
        public static bool TelemetryDisabled
        {
            get => Settings.Telemetry.Disable;

            set
            {
                Settings.Telemetry.Disable = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to disable clean up.
        /// </summary>
        public static bool CleanUpDisabled
        {
            get => Settings.CleanUp.Disable;

            set
            {
                Settings.CleanUp.Disable = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the interval in days to clean up old files and directories.
        /// </summary>
        public static int CleanUpDays
        {
            get => Settings.CleanUp.IntervalInDays;

            set
            {
                Settings.CleanUp.IntervalInDays = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets the owner of the winget-pkgs repository.
        /// </summary>
        public static string WindowsPackageManagerRepositoryOwner
        {
            get => Settings.WindowsPackageManagerRepository.Owner;

            set
            {
                Settings.WindowsPackageManagerRepository.Owner = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets the name of the winget-pkgs repository.
        /// </summary>
        public static string WindowsPackageManagerRepositoryName
        {
            get => Settings.WindowsPackageManagerRepository.Name;

            set
            {
                Settings.WindowsPackageManagerRepository.Name = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets the format of the manifest file.
        /// </summary>
        public static ManifestFormat ManifestFormat
        {
            get => Settings.Manifest.Format;

            set
            {
                Settings.Manifest.Format = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether paths displayed on the console are substituted with environment variables.
        /// </summary>
        public static bool AnonymizePaths
        {
            get => Settings.Visual.AnonymizePaths;

            set
            {
                Settings.Visual.AnonymizePaths = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the pull request should be opened automatically in the browser on submission.
        /// </summary>
        public static bool OpenPRInBrowser
        {
            get => Settings.PullRequest.OpenInBrowser;

            set
            {
                Settings.PullRequest.OpenInBrowser = value;
                SaveSettings();
            }
        }

        private static SettingsManifest Settings { get; set; }

        /// <summary>
        /// Attempts to parse a settings json file to determine if the file is valid based on the settings schema.
        /// </summary>
        /// <param name="path">Path to settings json file.</param>
        /// <param name="settingsManifest">Settings manifest parsed from the json file.</param>
        /// <returns>A boolean IsValid that indicates if the file was parsed successfully and a list of error strings if the parsing fails.</returns>
        public static (bool IsValid, List<string> Errors) ParseJsonFile(string path, out SettingsManifest settingsManifest)
        {
            List<string> errors = new List<string>();

            string json = File.ReadAllText(path);
            settingsManifest = JsonConvert.DeserializeObject<SettingsManifest>(
                json,
                new JsonSerializerSettings
                {
                    Error = (sender, args) =>
                    {
                        errors.Add(args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                    },
                });

            if (settingsManifest == null)
            {
                errors.Add("Manifest cannot be empty.");
            }

            bool isValid = errors.Count == 0;
            return (isValid, errors);
        }

        /// <summary>
        /// Checks if the tool is being launched for the first time.
        /// </summary>
        public static void FirstRunTelemetryConsent()
        {
            if (!File.Exists(SettingsJsonPath))
            {
                Console.WriteLine(Resources.TelemetrySettings_Message);
                Console.WriteLine("------------------");
                Console.WriteLine(Resources.TelemetryJustification_Message);
                Console.WriteLine(Resources.TelemetryAnonymous_Message);
                Console.WriteLine(Resources.TelemetryEnabledByDefault_Message);
                Console.WriteLine();
                TelemetryDisabled = false;
            }
        }

        /// <summary>
        /// Saves the current settings configurations to the settings.json file.
        /// </summary>
        /// <param name="settings">Optional settings manifest to save. If null, the current settings will be saved.</param>
        public static void SaveSettings(SettingsManifest settings = null)
        {
            if (settings != null)
            {
                Settings = settings;
            }

            Debug.Assert(Settings != null, "Settings should not be null when saving settings.");
            string output = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsJsonPath, output);
        }

        /// <summary>
        /// Gets the current settings as a Json object.
        /// </summary>
        /// <returns>A Json object representing the current settings.</returns>
        public static JObject ToJson()
        {
            return JObject.FromObject(Settings);
        }

        /// <summary>
        /// Loads the correct settings file based on the following order.
        /// 1. If the settings file exists and is valid, then overwrite the backup file with the current settings file.
        /// 2. If the backup settings file exists and is valid, then recreate the settings file using the backup.
        /// 3. Otherwise, use default settings based on the settings schema.
        /// </summary>
        private static void LoadSettings()
        {
            SettingsManifest parsedSettings;

            if (File.Exists(SettingsJsonPath) && ParseJsonFile(SettingsJsonPath, out parsedSettings).IsValid)
            {
                Settings = parsedSettings;
            }
            else if (File.Exists(SettingsBackupJsonPath) && ParseJsonFile(SettingsBackupJsonPath, out parsedSettings).IsValid)
            {
                Logger.WarnLocalized(nameof(Resources.UnexpectedErrorLoadSettings_Message));
                Logger.WarnLocalized(nameof(Resources.LoadSettingsFromBackup_Message));
                Settings = parsedSettings;
            }
            else
            {
                // If either of these files exist, then this indicates that a parsing error has occured and we can display warnings.
                if (File.Exists(SettingsJsonPath) || File.Exists(SettingsBackupJsonPath))
                {
                    Logger.WarnLocalized(nameof(Resources.UnexpectedErrorLoadSettings_Message));
                    Logger.WarnLocalized(nameof(Resources.LoadSettingsFromDefault_Message));
                }

                Settings = new SettingsManifest
                {
                    Telemetry = new Models.Settings.Telemetry(),
                    CleanUp = new CleanUp(),
                    WindowsPackageManagerRepository = new WindowsPackageManagerRepository(),
                    Manifest = new Manifest(),
                    Visual = new Visual(),
                };
            }
        }
    }
}
