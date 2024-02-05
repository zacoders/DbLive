namespace EasyFlow.Tests.Project;

public class SettingsTests
{
	[Fact]
	public void GetMigrationType()
	{
		var mockSet = new MockSet();

		string projectPath = @"C:\DB";
		mockSet.ProjectPath.ProjectPath.Returns(projectPath);
		mockSet.FileSystem.PathExistsAndNotEmpty(projectPath).Returns(true);

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

		var sqlProject = new SettingsAccessor(mockSet.ProjectPath, mockSet.FileSystem);

		var settings = sqlProject.ProjectSettings;

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
	}
}