// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Models.Settings;
    using Microsoft.WingetCreateCLI.Properties;
    using Newtonsoft.Json;

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
        public static bool TelemetryDisable
        {
            get
            {
                return Settings.Telemetry.Disable;
            }

            set
            {
                Settings.Telemetry.Disable = value;
                SaveSettings();
            }
        }

        private static SettingsManifest Settings { get; set; }

        /// <summary>
        /// Attempts to parse a settings json file to determine if the file is valid based on the settings schema.
        /// </summary>
        /// <param name="path">Path to settings json file.</param>
        /// <returns>A boolean IsValid that indicates if the file was parsed successfully and a list of error strings if the parsing fails.</returns>
        public static (bool IsValid, List<string> Errors) ParseJsonFile(string path)
        {
            SettingsManifest settingsManifest;
            List<string> errors = new List<string>();

            using (StreamReader r = new StreamReader(path))
            {
                string json = r.ReadToEnd();
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
            }

            if (settingsManifest == null)
            {
                errors.Add("Manifest cannot be empty.");
            }

            bool isValid = errors.Count == 0 && settingsManifest != null;
            return (isValid, errors);
        }

        /// <summary>
        /// Creates a settings json file based on the loaded setting configuration and outputs it to the specified path.
        /// </summary>
        /// <param name="path">Output path of settings json file.</param>
        public static void GenerateFileFromLoadedSettings(string path)
        {
            string output = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(path, output);
        }

        /// <summary>
        /// Loads a json file from a path and creates a SettingsManifest object model.
        /// </summary>
        /// <param name="path">Path to settings json file.</param>
        /// <returns>SettingsManifest object model.</returns>
        public static SettingsManifest LoadJsonFile(string path)
        {
            using (StreamReader r = new StreamReader(path))
            {
                string json = r.ReadToEnd();
                return JsonConvert.DeserializeObject<SettingsManifest>(json);
            }
        }

        /// <summary>
        /// Loads the correct settings file based on the following order.
        /// 1. If the settings file exists and is valid, then overwrite the backup file with the current settings file.
        /// 2. If the backup settings file exists and is valid, then recreate the settings file using the backup.
        /// 3. Otherwise, use default settings based on the settings schema.
        /// </summary>
        private static void LoadSettings()
        {
            if (File.Exists(SettingsJsonPath) && ParseJsonFile(SettingsJsonPath).IsValid)
            {
                Settings = LoadJsonFile(SettingsJsonPath);
            }
            else if (File.Exists(SettingsBackupJsonPath) && ParseJsonFile(SettingsBackupJsonPath).IsValid)
            {
                Logger.WarnLocalized(nameof(Resources.UnexpectedErrorLoadSettings_Message));
                Logger.WarnLocalized(nameof(Resources.LoadSettingsFromBackup_Message));
                Settings = LoadJsonFile(SettingsBackupJsonPath);
            }
            else
            {
                Logger.WarnLocalized(nameof(Resources.UnexpectedErrorLoadSettings_Message));
                Logger.WarnLocalized(nameof(Resources.LoadSettingsFromDefault_Message));
                Settings = new SettingsManifest { Telemetry = new Models.Settings.Telemetry() };
            }
        }

        private static void SaveSettings()
        {
            string output = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsJsonPath, output);
        }
    }
}
