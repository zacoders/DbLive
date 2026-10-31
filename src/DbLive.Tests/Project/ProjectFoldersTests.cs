namespace DbLive.Tests.Project;

public class ProjectFoldersTests
{
	[Theory]
	[InlineData('\\')]
	[InlineData('/')]
	public async Task Nested_Folder_Settings_Work_With_Any_Separator(char separator)
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			MigrationsFolder = $"db{separator}Migrations",
			CodeFolder = $"db{separator}Code",
			TestsFolder = $"db{separator}Tests",
			BeforeDeployFolder = $"db{separator}BeforeDeploy",
			AfterDeployFolder = $"db{separator}AfterDeploy"
		});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// folders do not exist, so each call only checks its folder path.
		_ = await sqlProject.GetMigrationsAsync();
		_ = await sqlProject.GetCodeGroupsAsync();
		_ = await sqlProject.GetTestsAsync();
		_ = await sqlProject.GetFolderItemsAsync(ProjectFolder.BeforeDeploy);
		_ = await sqlProject.GetFolderItemsAsync(ProjectFolder.AfterDeploy);

		// paths use the OS separator.
		string dbPath = projectPath.CombineWith("db");
		mockSet.FileSystem.Received(1).PathExists(dbPath.CombineWith("Migrations"));
		mockSet.FileSystem.Received(2).PathExists(dbPath.CombineWith("Code")); // code groups and tests
		mockSet.FileSystem.Received(1).PathExists(dbPath.CombineWith("Tests"));
		mockSet.FileSystem.Received(1).PathExists(dbPath.CombineWith("BeforeDeploy"));
		mockSet.FileSystem.Received(1).PathExists(dbPath.CombineWith("AfterDeploy"));
	}
}
