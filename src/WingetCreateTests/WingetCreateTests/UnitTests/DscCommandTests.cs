// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WingetCreateCLI.Commands;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Properties;
using NUnit.Framework;

/// <summary>
/// Unit test class for the DSC Command.
/// </summary>
public class DscCommandTests
{
    /// <summary>
    /// Execute the DSC command
    /// </summary>
    /// <param name="args">The arguments to pass to the DSC command.</param>
    /// <returns>Result of executing the DSC command.</returns>
    public static async Task<ExecuteResult> ExecuteDscCommandAsync(List<string> args)
    {
        var sw = new StringWriter();
        Console.SetOut(sw);
        var executeResult = await Parser.Default.ParseArguments<DscCommand>(args).Value.Execute();
        var output = sw.ToString();
        return new(executeResult, output);
    }

    /// <summary>
    /// OneTimeSetup method for the DSC command unit tests.
    /// </summary>
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Logger.Initialize();
    }

    [Test]
    public async Task DscSettingsResource_Success()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await ExecuteDscCommandAsync([command.CommandName, "--get", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task DscResourceMissing_ErrorMessage()
    {
        // Arrange
        List<BaseDscCommand> dscCommands = [new DscSettingsCommand()];

        // Act
        var result = await ExecuteDscCommandAsync([]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(string.Format(Resources.DscResourceMissing_Message, string.Join(", ", dscCommands.Select(c => c.CommandName)))));
    }

    [Test]
    public async Task DscResourceNotFound_ErrorMessage()
    {
        // Arrange
        var dscResourceName = "ResourceNotFound";

        // Act
        var result = await ExecuteDscCommandAsync([dscResourceName]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(string.Format(Resources.DscResourceNotFound_Message, dscResourceName)));
    }

    [Test]
    public async Task DscResourceInvalidOperation_ErrorMessage()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await ExecuteDscCommandAsync([command.CommandName]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationInvalid_Message));
    }

    [Test]
    public async Task DscSettingsResourceFailedOperation_ErrorMessage()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await ExecuteDscCommandAsync([command.CommandName, "--set", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationFailed_Message));
    }

    /// <summary>
    /// Result of executing a DSC command.
    /// </summary>
    /// <param name="Success">Value indicating whether the command execution was successful.</param>
    /// <param name="Output">Value containing the output of the command execution.</param>
    public record class ExecuteResult(bool Success, string Output);
}
