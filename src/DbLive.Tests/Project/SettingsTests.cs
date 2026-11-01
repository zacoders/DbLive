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
}
