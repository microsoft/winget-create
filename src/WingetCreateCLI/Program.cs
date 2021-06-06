// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CommandLine;
    using CommandLine.Text;
    using Microsoft.WingetCreateCLI.Commands;
    using Microsoft.WingetCreateCLI.Logging;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCLI.Telemetry;
    using Microsoft.WingetCreateCLI.Telemetry.Events;
    using Microsoft.WingetCreateCore.Common;

    /// <summary>
    /// Main entry class for the CLI.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Program name of the app.
        /// </summary>
        private const string ProgramName = "wingetcreate";

        private static async Task<int> Main(string[] args)
        {
            Logger.Initialize();

            string arguments = string.Join(' ', Environment.GetCommandLineArgs());
            Logger.Trace($"Command line args: {arguments}");

            Parser myParser = new Parser(config => config.HelpWriter = null);

            var parserResult = myParser.ParseArguments<NewCommand, UpdateCommand, SubmitCommand, TokenCommand>(args);
            BaseCommand command = parserResult.MapResult(c => c as BaseCommand, err => null);
            if (command == null)
            {
                DisplayHelp(parserResult as NotParsed<object>);
                return 1;
            }

            try
            {
                WingetCreateCore.Serialization.ProducedBy = string.Join(" ", ProgramName, Utils.GetEntryAssemblyVersion());
                return await command.Execute() ? 0 : 1;
            }
            catch (Exception ex)
            {
                TelemetryManager.Log.WriteEvent(new GlobalExceptionEvent
                {
                    ExceptionType = ex.GetType().ToString(),
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                });

                Logger.Error(ex.ToString());
                return 1;
            }
        }

        private static void DisplayHelp(NotParsed<object> result)
        {
            var helpText = HelpText.AutoBuild(
                result,
                h =>
                {
                    h.AddDashesToOption = true;
                    h.AdditionalNewLineAfterOption = false;
                    h.Heading = string.Format(Resources.Heading, Utils.GetEntryAssemblyVersion()) + Environment.NewLine;
                    h.Copyright = Constants.MicrosoftCopyright;
                    h.AddNewLineBetweenHelpSections = true;
                    h.AddPreOptionsLine(Resources.AppDescription_HelpText);
                    h.AddPostOptionsLines(new string[] { Resources.MoreHelp_HelpText, Resources.PrivacyStatement_HelpText });
                    h.MaximumDisplayWidth = 100;
                    h.AutoHelp = false;
                    h.AutoVersion = false;
                    return h;
                },
                e => e,
                verbsIndex: true);
            Console.WriteLine(helpText);
            Console.WriteLine();

            foreach (var error in result.Errors)
            {
                if (error is SetValueExceptionError sve)
                {
                    Utils.WriteLineColored(ConsoleColor.Red, $"{sve.NameInfo.LongName}: {sve.Exception.Message}");
                    if (sve.Exception.InnerException != null)
                    {
                        Utils.WriteLineColored(ConsoleColor.Red, $"{sve.Exception.InnerException.Message}");
                    }

                    if (sve.Value is IEnumerable<object> list)
                    {
                        foreach (var val in list)
                        {
                            Utils.WriteLineColored(ConsoleColor.Red, $"\t{val}");
                        }
                    }
                    else
                    {
                        Utils.WriteLineColored(ConsoleColor.Red, $"\t{sve.Value}");
                    }
                }
                else if (error is UnknownOptionError uoe)
                {
                    Utils.WriteLineColored(ConsoleColor.Red, $"Unknown option: {uoe.Token}");
                }
                else
                {
                    Utils.WriteLineColored(ConsoleColor.Red, $"Command line parsing error: {error.Tag}");
                }
            }
        }
    }
}
