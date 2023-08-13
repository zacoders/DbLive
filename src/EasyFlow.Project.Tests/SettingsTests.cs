namespace EasyFlow.Project.Tests;

[TestClass]
public class SettingsTests
{
	[TestMethod]
	public void GetMigrationType()
	{
		string projectPath = new Uri("c:/project1").LocalPath;
		string settingsPath = Path.Combine(projectPath, "settings.json");

		var mockSet = new MockSet();

		mockSet.FileSystem.Setup(fs => fs.FileExists(It.Is<string>(v => v == settingsPath))).Returns(true);

		mockSet.FileSystem.Setup(fs => fs.FileReadAllText(It.Is<string>(v => v == settingsPath)))
			.Returns("""
			{
				"TransactionLevel": "None"
			}
			""");

		var sqlProject = new EasyFlowProject(mockSet.FileSystem.Object);
		sqlProject.Load(projectPath);

		var settings = sqlProject.GetSettings();

		Assert.IsNotNull(settings);
		Assert.AreEqual(TransactionLevel.None, settings.TransactionLevel);
	}
}