// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Newtonsoft.Json;

/// <summary>
/// Result of executing a DSC command.
/// </summary>
public class DscExecuteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DscExecuteResult"/> class.
    /// </summary>
    /// <param name="success">Value indicating whether the command execution was successful.</param>
    /// <param name="output">Output stream content.</param>
    /// <param name="error">Error stream content.</param>
    public DscExecuteResult(bool success, string output, string error)
    {
        this.Success = success;
        this.Output = output;
        this.Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the command execution was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the output stream content of the operation.
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Gets the error stream content of the operation.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// Gets the messages from the error stream.
    /// </summary>
    /// <returns>List of messages with their levels.</returns>
    public List<(DscMessageLevel Level, string Message)> Messages()
    {
        var lines = this.Error.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        return lines.SelectMany(line =>
        {
            var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(line);
            return map.Select(kvp => (this.GetMessageLevel(kvp.Key), kvp.Value)).ToList();
        }).ToList();
    }

    /// <summary>
    /// Gets the output as settings state.
    /// </summary>
    /// <returns>Settings state.</returns>
    public SettingsResourceObject OutputState()
    {
        var lines = this.Output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        Debug.Assert(lines.Length == 1, "Output should contain exactly one line.");
        return JsonConvert.DeserializeObject<SettingsResourceObject>(lines[0]);
    }

    /// <summary>
    /// Gets the output as settings state and diff.
    /// </summary>
    /// <returns>Settings state and diff.</returns>
    public (SettingsResourceObject State, List<string> Diff) OutputStateAndDiff()
    {
        var lines = this.Output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        Debug.Assert(lines.Length == 2, "Output should contain exactly two lines.");
        var settingsObject = JsonConvert.DeserializeObject<SettingsResourceObject>(lines[0]);
        var diff = JsonConvert.DeserializeObject<List<string>>(lines[1]);
        return (settingsObject, diff);
    }

    /// <summary>
    /// Gets the message level from a string representation.
    /// </summary>
    /// <param name="level">The string representation of the message level.</param>
    /// <returns>The level as <see cref="DscMessageLevel"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the level is unknown.</exception>
    private DscMessageLevel GetMessageLevel(string level)
    {
        return level switch
        {
            "error" => DscMessageLevel.Error,
            "warn" => DscMessageLevel.Warning,
            "info" => DscMessageLevel.Info,
            "debug" => DscMessageLevel.Debug,
            "trace" => DscMessageLevel.Trace,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown message level"),
        };
    }
}
