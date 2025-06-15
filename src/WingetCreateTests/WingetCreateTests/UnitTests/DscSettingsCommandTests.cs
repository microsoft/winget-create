// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests;

using System.Threading.Tasks;
using Microsoft.WingetCreateCLI;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Microsoft.WingetCreateCLI.Models.Settings;
using Microsoft.WingetCreateCLI.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// Unit test class for the DSC settings Command.
/// </summary>
public class DscSettingsCommandTests
{
    private const string MockName = "mock_name";
    private const string MockOwner = "mock_owner";
    private JToken rawOriginalSettings;

    /// <summary>
    /// Gets the settings state with default values.
    /// </summary>
    private SettingsManifest DefaultSettings => new();

    /// <summary>
    /// Gets the settings state after the test is run.
    /// </summary>
    private SettingsManifest CurrentSettings => UserSettings.ToJson().ToObject<SettingsManifest>();

    /// <summary>
    /// Gets the settings state before the test is run.
    /// </summary>
    private SettingsManifest OriginalSettings => this.rawOriginalSettings.ToObject<SettingsManifest>();

    /// <summary>
    /// OneTimeSetup method for the DSC command unit tests.
    /// </summary>
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Logger.Initialize();
    }

    /// <summary>
    /// Setup method for the cache command unit tests.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.rawOriginalSettings = UserSettings.ToJson();
    }

    /// <summary>
    /// Teardown method for each individual test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        UserSettings.SaveSettings(this.OriginalSettings);
    }

    [Test]
    public async Task DscSettingsResource_Get_Success()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await DscCommandTests.ExecuteDscCommandAsync([command.CommandName, "--get", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain(this.CreateGetResponse()));
    }

    [Test]
    public async Task DscSettingsResource_Export_Success()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await DscCommandTests.ExecuteDscCommandAsync([command.CommandName, "--export", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain(this.CreateGetResponse()));
    }

    [Test]
    public async Task DscSettingsResource_SetEmpty_Fail()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await DscCommandTests.ExecuteDscCommandAsync([command.CommandName, "--set", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationFailed_Message));
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task DscSettingsResource_Set_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();

        // Part 1: Update settings repo name only
        {
            // Act
            var setRepoName = await DscCommandTests.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(name: MockName, isPartial: isPartial)]);

            // Assert
            Assert.That(setRepoName.Success, Is.True);
            this.AssertSettingsRepoHasChanged(name: MockName);
        }

        // Part 2: Now update settings repo owner only
        {
            // Act
            var setRepoOwner = await DscCommandTests.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(owner: MockOwner, isPartial: isPartial)]);

            // Assert
            Assert.That(setRepoOwner.Success, Is.True);
            this.AssertSettingsRepoHasChanged(name: (isPartial ?? true) ? MockName : null, owner: MockOwner);
        }
    }

    [Test]
    public async Task DscSettingsResource_Test_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();
        var currentSettings = this.CurrentSettings;
        currentSettings.WindowsPackageManagerRepository.Name = MockName;
        UserSettings.SaveSettings(currentSettings);

        // TODO
    }

    private void ResetSettingsToDefaultValues()
    {
        UserSettings.SaveSettings(this.DefaultSettings);
    }

    private string CreateGetResponse(bool? isPartial = null)
    {
        return this.CreateResourceObject(UserSettings.ToJson(), isPartial);
    }

    private string CreateInput(string name = null, string owner = null, bool? isPartial = null)
    {
        var repo = new JObject();
        if (name != null)
        {
            repo["name"] = name;
        }

        if (owner != null)
        {
            repo["owner"] = owner;
        }

        var input = new JObject
        {
            [nameof(WindowsPackageManagerRepository)] = repo,
        };

        return this.CreateResourceObject(input, isPartial);
    }

    private string CreateResourceObject(JObject settings, bool? isPartial)
    {
        var resourceObject = new SettingsResourceObject
        {
            Settings = settings,
            Action = isPartial.HasValue ? (isPartial.Value ? SettingsResourceObject.ActionPartial : SettingsResourceObject.ActionFull) : null,
        };
        return JObject.FromObject(resourceObject).ToString(Formatting.None);
    }

    private void AssertSettingsRepoHasChanged(string name = null, string owner = null)
    {
        var currentSettings = this.CurrentSettings;
        var defaultSettings = this.DefaultSettings;
        if (name != null)
        {
            defaultSettings.WindowsPackageManagerRepository.Name = name;
        }

        if (owner != null)
        {
            defaultSettings.WindowsPackageManagerRepository.Owner = owner;
        }

        var currentSettingsJson = JToken.FromObject(currentSettings);
        var defaultSettingsJson = JToken.FromObject(defaultSettings);
        Assert.That(JToken.DeepEquals(currentSettingsJson, defaultSettingsJson), Is.True);
    }
}
