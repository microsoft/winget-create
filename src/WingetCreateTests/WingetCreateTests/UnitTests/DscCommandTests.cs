// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Properties;
using Microsoft.WingetCreateTests;
using NUnit.Framework;

/// <summary>
/// Unit test class for the DSC Command.
/// </summary>
public class DscCommandTests
{
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
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--get", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task DscResourceMissing_ErrorMessage()
    {
        // Arrange
        List<BaseDscCommand> dscCommands = [new DscSettingsCommand()];

        // Act
        var result = await TestUtils.ExecuteDscCommandAsync([]);

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
        var result = await TestUtils.ExecuteDscCommandAsync([dscResourceName]);

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
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName]);

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
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationFailed_Message));
    }
}
