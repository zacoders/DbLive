namespace DbLive.Tests.Project;

public class SettingsTests
{
	[Fact]
	public void LoadSettings()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(true);

		mockSet.FileSystem.FileReadAllText(settingsPath)
			.Returns("""
				{
					"TransactionWrapLevel": "None"
				}
				"""
			);

		var settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		var settings = settingsAccessor.GetProjectSettings;

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);


		Assert.Equal(settings, settingsAccessor.GetProjectSettings);
	}

	[Fact]
	public void DefaultSettings()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(false);

		var settingsAccessor = mockSet.CreateUsingMocks<SettingsAccessor>();

		var settings = settingsAccessor.GetProjectSettings;

		Assert.NotNull(settings);
	}
}