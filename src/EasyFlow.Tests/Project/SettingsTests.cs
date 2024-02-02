namespace EasyFlow.Tests.Project;

public class SettingsTests
{
	[Fact]
	public void GetMigrationType()
	{
		var mockSet = new MockSet();

		mockSet.ProjectPath.ProjectPath.Returns(@"C:\DB\");
		mockSet.FileSystem.PathExistsAndNotEmpty(@"C:\DB\").Returns(true);

		string settingsPath = @"C:\DB\settings.json";

		mockSet.FileSystem.FileExists(settingsPath).Returns(true);

		mockSet.FileSystem.FileReadAllText(settingsPath)
			.Returns(
				"""
					{
						"TransactionWrapLevel": "None"
					}
				"""
			);

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var settings = sqlProject.GetSettings();

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
	}
}