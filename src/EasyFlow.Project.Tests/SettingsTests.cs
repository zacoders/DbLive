namespace EasyFlow.Project.Tests;

public class SettingsTests
{
	[Fact]
	public void GetMigrationType()
	{
		string projectPath = new Uri("c:/project1").LocalPath;
		string settingsPath = Path.Combine(projectPath, "settings.json");

		var mockSet = new MockSet();

		mockSet.FileSystem.FileExists(Arg.Is<string>(v => v == settingsPath)).Returns(true);

		mockSet.FileSystem.FileReadAllText(Arg.Is<string>(v => v == settingsPath))
			.Returns("""
			{
				"TransactionWrapLevel": "None"
			}
			""");

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load(projectPath);

		var settings = sqlProject.GetSettings();

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
	}
}