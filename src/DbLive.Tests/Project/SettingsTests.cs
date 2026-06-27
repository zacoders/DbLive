namespace DbLive.Tests.Project;

public class SettingsTests
{
	[Fact]
	public async Task LoadSettings()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(true);

		mockSet.FileSystem.FileReadAllTextAsync(settingsPath)
			.Returns("""
				{
					"TransactionWrapLevel": "None"
				}
				"""
			);

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

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(false);

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.NotNull(settings);
	}

	[Fact]
	public async Task LoadSettings_WithProjectId()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(true);

		mockSet.FileSystem.FileReadAllTextAsync(settingsPath)
			.Returns("""
				{
					"ProjectId": "my-operational-db"
				}
				"""
			);

		SettingsAccessor settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		DbLiveSettings settings = await settingsAccessor.GetProjectSettingsAsync();

		Assert.Equal("my-operational-db", settings.ProjectId);
	}
}