// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using System;
using System.Collections.Generic;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Base class for DSC commands.
/// </summary>
public abstract class BaseDscCommand
{
    /// <summary>
    /// Tries to create an instance of a DSC command based on the command name.
    /// </summary>
    /// <param name="commandName">The name of the command to create an instance for.</param>
    /// <param name="commandInstance">The created command instance if successful; otherwise, null.</param>
    /// <returns>True if the command instance was created successfully; otherwise, false.</returns>
    public static bool TryCreateInstance(string commandName, out BaseDscCommand commandInstance)
    {
        var formattedCommandName = commandName?.ToLowerInvariant() ?? string.Empty;
        switch (formattedCommandName)
        {
            case DscSettingsCommand.CommandName:
                commandInstance = new DscSettingsCommand();
                return true;

            // Add more cases here for other DSC commands as needed.

            // Return false if no matching command is found.
            default:
                commandInstance = null;
                return false;
        }
    }

    /// <summary>
    /// Gets the list of available command names for DSC commands.
    /// </summary>
    /// <returns>The list of available command names.</returns>
    public static List<string> GetAvailableCommands()
    {
        return [
            DscSettingsCommand.CommandName,

            // Add more command names here as needed.
        ];
    }

    /// <summary>
    /// DSC Get command.
    /// </summary>
    /// <param name="input">Input for the Get command.</param>
    /// <returns>True if the command was successful; otherwise, false.</returns>
    public abstract bool Get(JToken input);

    /// <summary>
    /// DSC Set command.
    /// </summary>
    /// <param name="input">Input for the Set command.</param>
    /// <returns>True if the command was successful; otherwise, false.</returns>
    public abstract bool Set(JToken input);

    /// <summary>
    /// DSC Test command.
    /// </summary>
    /// <param name="input">Input for the Test command.</param>
    /// <returns>True if the command was successful; otherwise, false.</returns>
    public abstract bool Test(JToken input);

    /// <summary>
    /// DSC Export command.
    /// </summary>
    /// <param name="input">Input for the Export command.</param>
    /// <returns>True if the command was successful; otherwise, false.</returns>
    public abstract bool Export(JToken input);

    /// <summary>
    /// DSC Schema command.
    /// </summary>
    /// <returns>True if the command was successful; otherwise, false.</returns>
    public abstract bool Schema();

    /// <summary>
    /// Writes a JSON output line to the console.
    /// </summary>
    /// <param name="token">The JSON token to be written.</param>
    protected static void WriteJsonOutputLine(JToken token)
    {
        Console.WriteLine(token.ToString(Formatting.None));
    }

    /// <summary>
    /// Writes a JSON output line to the error stream.
    /// </summary>
    /// <param name="level">The message level.</param>
    /// <param name="message">The message to be written.</param>
    protected static void WriteMessageOutputLine(DscMessageLevel level, string message)
    {
        var token = new JObject
        {
            [GetMessageLevel(level)] = message,
        };
        Console.Error.WriteLine(token.ToString(Formatting.None));
    }

    /// <summary>
    /// Creates a Json schema for a DSC resource object.
    /// </summary>
    /// <typeparam name="T">The type of the resource object.</typeparam>
    /// <param name="commandName">The name of the command for which the schema is being created.</param>
    /// <returns>A Json object representing the schema.</returns>
    protected JObject CreateSchema<T>(string commandName)
    where T : BaseResourceObject, new()
    {
        var resourceObject = new T();
        return new JObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = commandName,
            ["type"] = "object",
            ["properties"] = resourceObject.GetProperties(),
            ["required"] = resourceObject.GetRequiredProperties(),
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// Gets the message level as a string based on the provided DscMessageLevel enum value.
    /// </summary>
    /// <param name="level">The DscMessageLevel enum value.</param>
    /// <returns>A string representation of the message level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the provided message level is not recognized.</exception>
    private static string GetMessageLevel(DscMessageLevel level)
    {
        return level switch
        {
            DscMessageLevel.Error => "error",
            DscMessageLevel.Warning => "warn",
            DscMessageLevel.Info => "info",
            DscMessageLevel.Debug => "debug",
            DscMessageLevel.Trace => "trace",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
    }
}
