using EasyFlow.Common;

namespace EasyFlow.Project.Tests;

public class SettingsTests
{
	[Fact]
	public void GetMigrationType()
	{
		string projectPath = "c:/project1";
		string settingsPath = projectPath.CombineWith("settings.json");

		var mockSet = new MockSet();

		mockSet.FileSystem.FileExists(Arg.Is<string>(v => v == settingsPath)).Returns(true);

		mockSet.FileSystem.ReadFileData(Arg.Is<string>(v => v == settingsPath))
			.Returns(
			new FileData
			{
				FilePath = settingsPath,
				Content = """
					{
						"TransactionWrapLevel": "None"
					}
				"""
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load(projectPath);

		var settings = sqlProject.GetSettings();

		Assert.NotNull(settings);
		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
	}
}