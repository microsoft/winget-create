// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateCLI.Commands.DscCommands;

using System;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Base class for DSC commands.
/// </summary>
public abstract class BaseDscCommand
{
    /// <summary>
    /// Gets the name of the command used to access the DSC functionality.
    /// </summary>
    public virtual string CommandName { get; }

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
    /// Creates a Json schema for a DSC resource object.
    /// </summary>
    /// <returns>A Json object representing the schema.</returns>
    protected JObject CreateSchema<T>()
    where T : BaseResourceObject, new()
    {
        var resourceObject = new T();
        return new JObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = this.CommandName,
            ["type"] = "object",
            ["properties"] = resourceObject.GetProperties(),
            ["required"] = resourceObject.GetRequiredProperties(),
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// Writes a JSON output line to the console.
    /// </summary>
    /// <param name="token">The JSON token to be written.</param>
    protected void WriteJsonOutputLine(JToken token)
    {
        Console.WriteLine(token.ToString(Formatting.None));
    }
}
