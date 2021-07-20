// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using CommandLine;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore;

    /// <summary>
    /// Command to manage downloaded installers found in the %TEMP%/wingetcreate folder.
    /// </summary>
    [Verb("cache", HelpText = "CacheCommand_HelpText", ResourceType = typeof(Resources))]
    public class CacheCommand : BaseCommand
    {
        /// <summary>
        /// Gets or sets a value indicating whether to clear the cached GitHub token.
        /// </summary>
        [Option('c', "clean", SetName = nameof(Clean), Required = true, HelpText = "Clean_HelpText", ResourceType = typeof(Resources))]
        public bool Clean { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to set the cached GitHub token.
        /// </summary>
        [Option('l', "list", Required = true, SetName = nameof(List), HelpText = "List_HelpText", ResourceType = typeof(Resources))]
        public bool List { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to open the folder containing the cached downloaded installers.
        /// </summary>
        [Option('o', "open", Required = true, SetName = nameof(Open), HelpText = "Open_HelpText", ResourceType = typeof(Resources))]
        public bool Open { get; set; }

        /// <summary>
        /// Executes the cache command flow.
        /// </summary>
        /// <returns>Boolean representing success or fail of the command.</returns>
        public override Task<bool> Execute()
        {
            CommandExecutedEvent commandEvent = new CommandExecutedEvent
            {
                Command = nameof(CacheCommand),
            };

            try
            {
                if (!Directory.Exists(PackageParser.InstallerDownloadPath))
                {
                    Directory.CreateDirectory(PackageParser.InstallerDownloadPath);
                }

                if (this.Clean)
                {
                    Logger.Info($"Cleaning up installers in {PackageParser.InstallerDownloadPath}");
                    Directory.Delete(PackageParser.InstallerDownloadPath, true);
                    commandEvent.IsSuccessful = true;
                }
                else if (this.List)
                {
                    string[] files = Directory.GetFiles(PackageParser.InstallerDownloadPath);
                    Logger.Info($"{files.Length} installers found in {PackageParser.InstallerDownloadPath}");
                    Console.WriteLine();

                    foreach (string file in files)
                    {
                        Logger.Info(Path.GetFileName(file));
                    }
                }
                else if (this.Open)
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = PackageParser.InstallerDownloadPath,
                        UseShellExecute = true,
                    });
                }

                return Task.FromResult(commandEvent.IsSuccessful);
            }
            finally
            {
                TelemetryManager.Log.WriteEvent(commandEvent);
            }
        }
    }
}
