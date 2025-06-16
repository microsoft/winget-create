// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <param name="output">Output of the command execution.</param>
    public DscExecuteResult(bool success, string output)
    {
        this.Success = success;
        this.Output = output;
    }

    /// <summary>
    /// Gets a value indicating whether the command execution was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the output result of the operation.
    /// </summary>
    public string Output { get; }

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
}
