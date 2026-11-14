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
