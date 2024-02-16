// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Logging
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.WingetCreateCLI.Properties;
    using Microsoft.WingetCreateCore.Common;
    using NLog;
    using NLog.Conditions;
    using NLog.Config;
    using NLog.Targets;

    /// <summary>
    /// Wrapper class for logging events and writing to console.
    /// </summary>
    public static class Logger
    {
        private static readonly CultureInfo USCulture = new("en-US");

        private static readonly FileTarget FileTarget = new()
        {
            FileName = @$"{Path.Combine(Common.LocalAppStatePath, Constants.DiagnosticOutputDirectoryFolderName)}\WingetCreateLog-{DateTime.Now:yyyy-MM-dd-HH-mm.fff}.txt",

            // Current layout example: 2021-01-01 08:30:59.0000|INFO|Microsoft.WingetCreateCLI.Commands.NewCommand.Execute|Log Message Example
            Layout = "${longdate}|${level:uppercase=true}|${callsite}|${message}",
        };

        private static readonly ColoredConsoleTarget ConsoleTarget = new() { Layout = "${message}" };

        private static NLog.Logger loggerConsole;
        private static NLog.Logger loggerFile;

        /// <summary>
        /// Initializes the Logger configuration settings.
        /// </summary>
        public static void Initialize()
        {
            // Add rules to file log configuration
            var loggerFileFactory = new LogFactory().Setup().LoadConfiguration(builder =>
            {
                builder.Configuration.AddRule(LogLevel.Trace, LogLevel.Fatal, FileTarget, "File");
            });

            // Get logger for file
            loggerFile = loggerFileFactory.GetLogger("File");

            // Add rules to console log configuration
            var loggerConsoleFactory = new LogFactory().Setup().LoadConfiguration(builder =>
            {
                builder.Configuration.AddRule(LogLevel.Debug, LogLevel.Fatal, ConsoleTarget, "Console");
            });

            // Set color for specific log level
            SetColorForConsoleTarget(ConsoleTarget, LogLevel.Info, ConsoleOutputColor.Green);
            SetColorForConsoleTarget(ConsoleTarget, LogLevel.Warn, ConsoleOutputColor.Yellow);
            SetColorForConsoleTarget(ConsoleTarget, LogLevel.Error, ConsoleOutputColor.Red);

            // Get logger for console
            loggerConsole = loggerConsoleFactory.GetLogger("Console");
        }

        /// <summary>
        /// Logs a message with the Trace log level. Does not write to console.
        /// </summary>
        /// <param name="message">Logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void Trace(string message, params object[] args) => Log(LogLevel.Trace, message, args);

        /// <summary>
        /// Logs a message with the Debug log level.
        /// </summary>
        /// <param name="message">Logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void Debug(string message, params object[] args) => Log(LogLevel.Debug, message, args);

        /// <summary>
        /// Logs a message with the Debug log level.
        /// </summary>
        /// <param name="resourceName">Resource name for logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void DebugLocalized(string resourceName, params object[] args) => LogLocalized(LogLevel.Debug, resourceName, args);

        /// <summary>
        /// Logs a message with the Info log level. Writes to console in GREEN.
        /// </summary>
        /// <param name="message">Logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void Info(string message, params object[] args) => Log(LogLevel.Info, message, args);

        /// <summary>
        /// Logs a message with the Info log level. Writes to console in GREEN.
        /// </summary>
        /// <param name="resourceName">Resource name for logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void InfoLocalized(string resourceName, params object[] args) => LogLocalized(LogLevel.Info, resourceName, args);

        /// <summary>
        /// Logs a message with the WARN log level. Writes to console in YELLOW.
        /// </summary>
        /// <param name="message">Logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void Warn(string message, params object[] args) => Log(LogLevel.Warn, message, args);

        /// <summary>
        /// Logs a message with the WARN log level. Writes to console in YELLOW.
        /// </summary>
        /// <param name="resourceName">Resource name for logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void WarnLocalized(string resourceName, params object[] args) => LogLocalized(LogLevel.Warn, resourceName, args);

        /// <summary>
        /// Logs a message with the ERROR log level. Writes to console in RED.
        /// </summary>
        /// <param name="message">Logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void Error(string message, params object[] args) => Log(LogLevel.Error, message, args);

        /// <summary>
        /// Logs a message with the ERROR log level. Writes to console in RED.
        /// </summary>
        /// <param name="resourceName">Resource name for logger message.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        public static void ErrorLocalized(string resourceName, params object[] args) => LogLocalized(LogLevel.Error, resourceName, args);

        /// <summary>
        /// Main logging method that creates a LogEventInfo and writes to the log.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="format">Formatted message to be written to the log.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        private static void Log(LogLevel level, string format, params object[] args)
        {
            string message = args.Length > 0 ? string.Format(format, args) : format;

            LogEventInfo logEventFile = new LogEventInfo(level, loggerFile.Name, message);
            loggerFile.Log(typeof(Logger), logEventFile);

            LogEventInfo logEventConsole = new LogEventInfo(level, loggerConsole.Name, message);
            loggerConsole.Log(typeof(Logger), logEventConsole);
        }

        /// <summary>
        /// Main logging method that creates a LogEventInfo and writes to the log.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="resourceName">Resource name for message to be written to the log.</param>
        /// <param name="args">Arguments to replace formatted items in message.</param>
        private static void LogLocalized(LogLevel level, string resourceName, params object[] args)
        {
            string localizedMessage = string.Format(Resources.ResourceManager.GetString(resourceName), args);
            string englishMessage = string.Format(Resources.ResourceManager.GetString(resourceName, USCulture), args);

            var logEventFile = new LogEventInfo(level, loggerFile.Name, englishMessage);
            loggerFile.Log(typeof(Logger), logEventFile);

            var logEventConsole = new LogEventInfo(level, loggerConsole.Name, localizedMessage);
            loggerConsole.Log(typeof(Logger), logEventConsole);
        }

        private static void SetColorForConsoleTarget(ColoredConsoleTarget target, LogLevel level, ConsoleOutputColor color)
        {
            var highlightRule = new ConsoleRowHighlightingRule();
            string expression = $"level == LogLevel.{level}";
            highlightRule.Condition = ConditionParser.ParseExpression(expression);
            highlightRule.ForegroundColor = color;
            target.RowHighlightingRules.Add(highlightRule);
        }
    }
}
