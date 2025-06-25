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

    /// <summary>
    /// Tests the a successful execution of the dsc command.
    /// </summary>
    /// <returns>Async Task.</returns>
    [Test]
    public async Task DscSettingsResource_Success()
    {
        // Act
        var result = await TestUtils.ExecuteDscCommandAsync(DscSettingsCommand.CommandName, "--get");

        // Assert
        Assert.That(result.Success, Is.True);
    }

    /// <summary>
    /// Tests the error message when a DSC resource is not found.
    /// </summary>
    /// <returns>Async Task.</returns>
    [Test]
    public async Task DscResourceNotFound_ErrorMessage()
    {
        // Arrange
        var dscResourceName = "ResourceNotFound";
        var availableResources = string.Join(", ", BaseDscCommand.GetAvailableCommands());

        // Act
        var result = await TestUtils.ExecuteDscCommandAsync(dscResourceName);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(string.Format(Resources.DscResourceNameNotFound_Message, dscResourceName, availableResources)));
    }

    /// <summary>
    /// Tests the error message when a DSC resource operation is not specified.
    /// </summary>
    /// <returns>Async Task.</returns>
    [Test]
    public async Task DscResourceOperationNotSpecified_ErrorMessage()
    {
        // Act
        var result = await TestUtils.ExecuteDscCommandAsync(DscSettingsCommand.CommandName);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationNotSpecified_Message));
    }

    /// <summary>
    /// Tests the error message when a DSC resource operation fails.
    /// </summary>
    /// <returns>Async Task.</returns>
    [Test]
    public async Task DscSettingsResourceFailedOperation_ErrorMessage()
    {
        // Act
        var result = await TestUtils.ExecuteDscCommandAsync(DscSettingsCommand.CommandName, "--set", string.Empty);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationFailed_Message));
    }
}
