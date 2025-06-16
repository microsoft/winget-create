// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.WingetCreateUnitTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WingetCreateCLI;
using Microsoft.WingetCreateCLI.Commands.DscCommands;
using Microsoft.WingetCreateCLI.Logging;
using Microsoft.WingetCreateCLI.Models.DscModels;
using Microsoft.WingetCreateCLI.Models.Settings;
using Microsoft.WingetCreateCLI.Properties;
using Microsoft.WingetCreateTests;
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

    /// <summary>
    /// Tests the Get operation.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DscSettingsResource_Get_Success()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--get", string.Empty]);
        var state = result.OutputState();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain(this.CreateGetResponse()));
        Assert.That(state.Action, Is.Null);
        this.AssertStateAndSettingsAreEqual(this.CurrentSettings, state);
    }

    /// <summary>
    /// Tests the Export operation.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DscSettingsResource_Export_Success()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--export", string.Empty]);
        var state = result.OutputState();

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain(this.CreateGetResponse()));
        Assert.That(state.Action, Is.Null);
        this.AssertStateAndSettingsAreEqual(this.CurrentSettings, state);
    }

    /// <summary>
    /// Tests the Set operation with an empty input.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task DscSettingsResource_SetEmpty_Fail()
    {
        // Arrange
        var command = new DscSettingsCommand();

        // Act
        var result = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", string.Empty]);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Output, Does.Contain(Resources.DscResourceOperationFailed_Message));
    }

    /// <summary>
    /// Tests the Set operation with diff.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task DscSettingsResource_SetWithDiff_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();

        // Part 1: Update settings repo name only
        {
            // Act
            var setRepoName = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(name: MockName, isPartial: isPartial)]);
            var stateAndDiff = setRepoName.OutputStateAndDiff();

            // Assert
            Assert.That(setRepoName.Success, Is.True);
            this.AssertSettingsHasChanged(name: MockName);
            this.AssertStateAndSettingsAreEqual(this.CurrentSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.EqualTo(new List<string>() { "settings" }));
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }

        // Part 2: Now update settings repo owner only
        {
            // Act
            var setRepoOwner = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(owner: MockOwner, isPartial: isPartial)]);
            var stateAndDiff = setRepoOwner.OutputStateAndDiff();

            // Assert
            Assert.That(setRepoOwner.Success, Is.True);
            this.AssertSettingsHasChanged(name: (isPartial ?? true) ? MockName : null, owner: MockOwner);
            this.AssertStateAndSettingsAreEqual(this.CurrentSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.EqualTo(new List<string>() { "settings" }));
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }
    }

    /// <summary>
    /// Tests the Set operation without diff.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task DscSettingsResource_SetWithoutDiff_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();

        // Part 1: Update settings repo name only
        {
            // Arrange
            this.UpdateSettings(name: MockName);
            var currentSettingsBeforeExecute = this.CurrentSettings;

            // Act
            var setRepoName = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(name: MockName, isPartial: isPartial)]);
            var stateAndDiff = setRepoName.OutputStateAndDiff();

            // Assert
            Assert.That(setRepoName.Success, Is.True);
            this.AssertSettingsHasChanged(name: MockName);
            this.AssertStateAndSettingsAreEqual(currentSettingsBeforeExecute, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.Empty);
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }

        // Part 2: Now update settings repo owner only
        {
            // Arrange
            var defaultRepoName = this.DefaultSettings.WindowsPackageManagerRepository.Name;
            this.UpdateSettings(name: (isPartial ?? true) ? null : defaultRepoName, owner: MockOwner);
            var currentSettingsBeforeExecute = this.CurrentSettings;

            // Act
            var setRepoOwner = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--set", this.CreateInput(owner: MockOwner, isPartial: isPartial)]);
            var stateAndDiff = setRepoOwner.OutputStateAndDiff();

            // Assert
            Assert.That(setRepoOwner.Success, Is.True);
            this.AssertSettingsHasChanged(name: (isPartial ?? true) ? MockName : null, owner: MockOwner);
            this.AssertStateAndSettingsAreEqual(currentSettingsBeforeExecute, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.Empty);
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }
    }

    /// <summary>
    /// Tests the Test operation with diff.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task DscSettingsResource_TestWithDiff_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();

        // Part 1: Test settings repo name only
        {
            // Act
            var testRepoName = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--test", this.CreateInput(name: MockName, isPartial: isPartial)]);
            var stateAndDiff = testRepoName.OutputStateAndDiff();

            // Assert
            Assert.That(testRepoName.Success, Is.True);
            this.AssertSettingsAreEqual(this.DefaultSettings, this.CurrentSettings);
            this.AssertStateAndSettingsAreEqual(this.DefaultSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.EqualTo(new List<string>() { "settings" }));
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }

        // Part 2: Now test settings repo owner only
        {
            // Act
            var testRepoOwner = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--test", this.CreateInput(owner: MockOwner, isPartial: isPartial)]);
            var stateAndDiff = testRepoOwner.OutputStateAndDiff();

            // Assert
            Assert.That(testRepoOwner.Success, Is.True);
            this.AssertSettingsAreEqual(this.DefaultSettings, this.CurrentSettings);
            this.AssertStateAndSettingsAreEqual(this.DefaultSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.EqualTo(new List<string>() { "settings" }));
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }
    }

    /// <summary>
    /// Tests the Test operation without diff.
    /// </summary>
    /// <returns>Async task.</returns>
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public async Task DscSettingsResource_TestWithoutDiff_Success(bool? isPartial)
    {
        // Arrange
        this.ResetSettingsToDefaultValues();
        var command = new DscSettingsCommand();

        // Part 1: Test settings repo name only
        {
            // Arrange
            this.UpdateSettings(name: MockName);

            // Act
            var testRepoName = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--test", this.CreateInput(name: MockName, isPartial: isPartial)]);
            var stateAndDiff = testRepoName.OutputStateAndDiff();

            // Assert
            Assert.That(testRepoName.Success, Is.True);
            this.AssertStateAndSettingsAreEqual(this.CurrentSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.Empty);
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }

        // Part 2: Now test settings repo owner only
        {
            // Arrange
            var defaultRepoName = this.DefaultSettings.WindowsPackageManagerRepository.Name;
            this.UpdateSettings(name: (isPartial ?? true) ? null : defaultRepoName, owner: MockOwner);

            // Act
            var testRepoOwner = await TestUtils.ExecuteDscCommandAsync([command.CommandName, "--test", this.CreateInput(owner: MockOwner, isPartial: isPartial)]);
            var stateAndDiff = testRepoOwner.OutputStateAndDiff();

            // Assert
            Assert.That(testRepoOwner.Success, Is.True);
            this.AssertStateAndSettingsAreEqual(this.CurrentSettings, stateAndDiff.State);
            Assert.That(stateAndDiff.Diff, Is.Empty);
            this.AssertStateAction(stateAndDiff.State, isPartial);
        }
    }

    /// <summary>
    /// Resets the settings to default values.
    /// </summary>
    private void ResetSettingsToDefaultValues()
    {
        UserSettings.SaveSettings(this.DefaultSettings);
    }

    /// <summary>
    /// Updates the settings with the provided name and owner.
    /// </summary>
    /// <param name="name">Optional name for the repository.</param>
    /// <param name="owner">Optional owner for the repository.</param>
    private void UpdateSettings(string name = null, string owner = null)
    {
        var settings = this.CurrentSettings;
        if (name != null)
        {
            settings.WindowsPackageManagerRepository.Name = name;
        }

        if (owner != null)
        {
            settings.WindowsPackageManagerRepository.Owner = owner;
        }

        UserSettings.SaveSettings(settings);
    }

    /// <summary>
    /// Create the response for the Get operation.
    /// </summary>
    /// <param name="isPartial">Optional parameter to indicate if the response is partial.</param>
    /// <returns>A JSON string representing the response.</returns>
    private string CreateGetResponse(bool? isPartial = null)
    {
        return this.CreateResourceObject(UserSettings.ToJson(), isPartial);
    }

    /// <summary>
    /// Creates the operation input.
    /// </summary>
    /// <param name="name">Optional name for the repository.</param>
    /// <param name="owner">Optional owner for the repository.</param>
    /// <param name="isPartial">Optional parameter to indicate if the input is partial.</param>
    /// <returns>A JSON string representing the operation input.</returns>
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

    /// <summary>
    /// Create the resource object for the operation.
    /// </summary>
    /// <param name="settings">Settings to include in the resource object.</param>
    /// <param name="isPartial">Optional parameter to indicate if the resource object is partial.</param>
    /// <returns>A JSON string representing the resource object.</returns>
    private string CreateResourceObject(JObject settings, bool? isPartial)
    {
        var resourceObject = new SettingsResourceObject
        {
            Settings = settings,
            Action = isPartial.HasValue ? (isPartial.Value ? SettingsResourceObject.ActionPartial : SettingsResourceObject.ActionFull) : null,
        };
        return JObject.FromObject(resourceObject).ToString(Formatting.None);
    }

    /// <summary>
    /// Asserts that the current settings have changed based on the provided name and owner.
    /// </summary>
    /// <param name="name">Optional name for the repository.</param>
    /// <param name="owner">Optional owner for the repository.</param>
    private void AssertSettingsHasChanged(string name = null, string owner = null)
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

        this.AssertSettingsAreEqual(defaultSettings, currentSettings);
    }

    /// <summary>
    /// Asserts that the state and settings are equal.
    /// </summary>
    /// <param name="settings">Settings manifest to compare against.</param>
    /// <param name="state">Output state to compare.</param>
    private void AssertStateAndSettingsAreEqual(SettingsManifest settings, SettingsResourceObject state)
    {
        var stateSettings = state.Settings.ToObject<SettingsManifest>();
        this.AssertSettingsAreEqual(settings, stateSettings);
    }

    /// <summary>
    /// Asserts that the state action is as expected.
    /// </summary>
    /// <param name="state">Output state to check.</param>
    /// <param name="isPartial">Optional parameter to indicate if the state is partial.</param>
    private void AssertStateAction(SettingsResourceObject state, bool? isPartial = null)
    {
        if (!isPartial.HasValue || isPartial.Value)
        {
            Assert.That(state.Action, Is.EqualTo(SettingsResourceObject.ActionPartial));
        }
        else
        {
            Assert.That(state.Action, Is.EqualTo(SettingsResourceObject.ActionFull));
        }
    }

    /// <summary>
    /// Asserts that two settings manifests are equal.
    /// </summary>
    /// <param name="expected">Expected settings manifest.</param>
    /// <param name="actual">Actual settings manifest.</param>
    private void AssertSettingsAreEqual(SettingsManifest expected, SettingsManifest actual)
    {
        var expectedJson = JToken.FromObject(expected);
        var actualJson = JToken.FromObject(actual);
        Assert.That(JToken.DeepEquals(expectedJson, actualJson), Is.True);
    }
}
