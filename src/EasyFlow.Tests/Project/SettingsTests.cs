namespace EasyFlow.Tests.Project;

public class SettingsTests
{
	[Fact]
	public void GetMigrationType()
	{
		var mockSet = new MockSet();

		string projectPath = @"C:\DB";
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);

		string settingsPath = projectPath.CombineWith("settings.json");

		mockSet.FileSystem.FileExists(settingsPath).Returns(true);

		mockSet.FileSystem.FileReadAllText(settingsPath)
			.Returns(
				"""
					{
						"TransactionWrapLevel": "None"
					}
				"""
			);

		var sqlProject = new SettingsAccessor(mockSet.ProjectPathAccessor, mockSet.FileSystem);

		var settings = sqlProject.ProjectSettings;

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
	}
}