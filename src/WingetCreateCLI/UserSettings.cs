// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.IO;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCore.Models.Settings;
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
        public static bool TelemetryDisabled
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
        /// Recreates a settings json file using the default values from the settings schema model.
        /// </summary>
        public static void GenerateDefaultSettingsFile()
        {
            SettingsManifest defaultSettings = new SettingsManifest { Telemetry = new WingetCreateCore.Models.Settings.Telemetry() };
            string output = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);
            File.WriteAllText(SettingsJsonPath, output);
            File.WriteAllText(SettingsBackupJsonPath, output);
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
        /// Loads the correct settings file based on the following order.
        /// 1. If the settings file exists and is valid, then overwrite the backup file with the current settings file.
        /// 2. If the backup settings file exists and is valid, then recreate the settings file using the backup.
        /// 3. Otherwise, use default settings based on the settings schema.
        /// </summary>
        private static void LoadSettings()
        {
            if (File.Exists(SettingsJsonPath) && IsValidSettingsJson(SettingsJsonPath))
            {
                Logger.Trace("Using configurations from settings file.");
                File.Copy(SettingsJsonPath, SettingsBackupJsonPath, true);
                Settings = LoadJsonFile(SettingsJsonPath);
            }
            else if (File.Exists(SettingsBackupJsonPath) && IsValidSettingsJson(SettingsBackupJsonPath))
            {
                Logger.Trace("Unable to find settings file, using configurations from backup file.");
                Settings = LoadJsonFile(SettingsBackupJsonPath);
            }
            else
            {
                Logger.Trace("Both the settings and backup settings files are missing or invalid. Regenerating factory setting files.");
                Settings = new SettingsManifest { Telemetry = new WingetCreateCore.Models.Settings.Telemetry() };
            }
        }

        /// <summary>
        /// Returns a bool indicating whether the json is able to be successfully parsed using the model from the Settings schema.
        /// </summary>
        /// <param name="path">Path to settings json file.</param>
        /// <returns>Boolean value.</returns>
        private static bool IsValidSettingsJson(string path)
        {
            SettingsManifest settingsManifest;

            try
            {
                using (StreamReader r = new StreamReader(path))
                {
                    string json = r.ReadToEnd();
                    settingsManifest = JsonConvert.DeserializeObject<SettingsManifest>(json);
                }
            }
            catch (JsonException e)
            {
                Logger.Warn("Unexpected error while loading settings. Please verify your settings by running the settings command.");
                Logger.Warn($"The following failures were found validating the settings: {e.Message}");
                return false;
            }

            return settingsManifest != null;
        }

        private static void SaveSettings()
        {
            string output = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsJsonPath, output);
        }

        private static SettingsManifest LoadJsonFile(string path)
        {
            using (StreamReader r = new StreamReader(path))
            {
                string json = r.ReadToEnd();
                return JsonConvert.DeserializeObject<SettingsManifest>(json);
            }
        }
    }
}
