namespace DbLive.Tests.Project;

public class SettingsTests
{
	[Fact]
	public async Task LoadSettings()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("settings.json", """
			{
				"TransactionWrapLevel": "None"
			}
			""");

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);


		Assert.Equal(settings, await settingsAccessor.GetProjectSettingsAsync());
	}

	[Fact]
	public async Task DefaultSettings()
	{
		MockSet mockSet = new();

		// only the settings file in the root counts.
		mockSet.ScriptSource.AddFile("Code/settings.json", """{ "TransactionWrapLevel": "None" }""");

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.Migration, settings.TransactionWrapLevel);
	}

	[Fact]
	public async Task LoadSettings_WithProjectId()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("Settings.JSON", """
			{
				"ProjectId": "my-operational-db"
			}
			""");

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.Equal("my-operational-db", settings.ProjectId);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" \r\n\t")]
	public async Task LoadSettings_EmptyFile_DefaultSettings(string json)
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("settings.json", json);

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.Equal(TransactionWrapLevel.Migration, settings.TransactionWrapLevel);
	}

	[Theory]
	[InlineData("settings.json", """{ "MaxCodeDeployRetries": 5 }""", "'MaxCodeDeployRetries'")]
	// a property of DbLiveSettings, but only the tests set it: not a key of the file.
	[InlineData("settings.json", """{ "LogSelfDeploy": true }""", "'LogSelfDeploy'")]
	[InlineData("settings.json", """{ "transactionWrapLevel": "None" }""", "'transactionWrapLevel'")]
	[InlineData("settings.json", """{ "TransactionWrapLevel": "Nope" }""", "$.TransactionWrapLevel")]
	[InlineData("settings.json", """{ "TransactionWrapLevel": "None", }""", "trailing comma")]
	[InlineData("Settings.JSON", """{ "TransactionWrapLevel": """, "LineNumber")]
	public async Task LoadSettings_InvalidFile_ErrorNamesFile(string fileName, string json, string expectedDetail)
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile(fileName, json);

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		InvalidProjectSettingsException e = await Assert.ThrowsAsync<InvalidProjectSettingsException>(settingsAccessor.GetProjectSettingsAsync);

		Assert.Equal(fileName, e.RelativePath);
		Assert.StartsWith($"Invalid project settings file {fileName}: ", e.Message);
		Assert.Contains(expectedDetail, e.Message);
		Assert.IsAssignableFrom<System.Text.Json.JsonException>(e.InnerException);
	}

	[Fact]
	public async Task LoadSettings_FailedRead_ReadAgain()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("settings.json", """{ "TransactionWrapLevel": "None" }""")
			.FailNextRead("settings.json", new IOException("The file is being used by another process."));

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		// a file locked for a moment: the first deployment fails.
		_ = await Assert.ThrowsAsync<IOException>(settingsAccessor.GetProjectSettingsAsync);

		// the next deployment from the same builder reads the file again.
		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);

		// a successful read is kept.
		Assert.Same(settings, await settingsAccessor.GetProjectSettingsAsync());
		Assert.Equal(2, mockSet.ScriptSource.ReadCount("settings.json"));
	}
}
