namespace DbLive.Tests.Project;

public class FilesInSeveralProjectFoldersTests
{
	[Fact]
	public async Task Code_folder_root_with_files_of_other_folders_fails_every_read()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = ""
		});

		mockSet.ScriptSource.AddFiles(
			"GetUser.sql",
			"Migrations/001.migration.sql",
			"Tests/GetUser.test.sql",
			"BeforeDeploy/before.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// the deployment reads migrations first, so it stops before anything is deployed.
		FilesInSeveralProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetMigrationsAsync);

		Assert.Equal(3, ex.Count);
		Assert.Equal(["BeforeDeploy/before.sql", "Migrations/001.migration.sql", "Tests/GetUser.test.sql"], ex.RelativePaths);
		Assert.Contains(
			"""'Migrations/001.migration.sql' (MigrationsFolder: "Migrations", CodeFolder: ""), 'Tests/GetUser.test.sql' (CodeFolder: "", TestsFolder: "Tests").""",
			ex.Message
		);

		_ = await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetCodeGroupsAsync);
		_ = await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetCodeBreakingItemsAsync);
		_ = await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetTestsAsync);
		_ = await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(() => project.GetFolderItemsAsync(ProjectFolder.BeforeDeploy));
	}

	[Fact]
	public async Task Code_folder_root_with_code_files_only_works()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = ""
		});

		mockSet.ScriptSource.AddFiles(
			"settings.json",
			"GetUser.sql",
			"GetUser.test.sql",
			"dbo/GetOrder.sql",
			// hidden files of the other folders are not read.
			"Migrations/.gitkeep",
			"Tests/.DS_Store"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		CodeGroup codeGroup = Assert.Single(await project.GetCodeGroupsAsync());
		Assert.Equal(["dbo/GetOrder.sql", "GetUser.sql"], codeGroup.CodeItems.Select(i => i.FileData.RelativePath));
		Assert.Equal("GetUser.test.sql", Assert.Single(await project.GetTestsAsync()).FileData.RelativePath);
		Assert.Empty(await project.GetMigrationsAsync());
	}

	[Theory]
	[InlineData("Code/Tests")]
	[InlineData(@"Code\Tests")]
	[InlineData("code/tests/")]
	public async Task Project_folder_inside_another_fails(string testsFolder)
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TestsFolder = testsFolder
		});

		mockSet.ScriptSource.AddFiles("Code/GetUser.sql", "Code/Tests/GetUser.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesInSeveralProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetTestsAsync);

		Assert.Equal(["Code/Tests/GetUser.sql"], ex.RelativePaths);
		Assert.Contains($"""'Code/Tests/GetUser.sql' (CodeFolder: "Code", TestsFolder: "{testsFolder.Replace('\\', '/').Trim('/')}").""", ex.Message);
	}

	[Fact]
	public async Task Project_folder_inside_another_without_files_works()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TestsFolder = "Code/Tests"
		});

		mockSet.ScriptSource.AddFiles("Code/GetUser.sql", "Code/GetUser.test.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		_ = Assert.Single(Assert.Single(await project.GetCodeGroupsAsync()).CodeItems);
		_ = Assert.Single(await project.GetTestsAsync());
	}

	[Fact]
	public async Task Two_settings_with_one_folder_fail()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			BeforeDeployFolder = "Deploy",
			AfterDeployFolder = "deploy"
		});

		mockSet.ScriptSource.AddFiles("Deploy/query-store.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesInSeveralProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(() => project.GetFolderItemsAsync(ProjectFolder.AfterDeploy));

		Assert.Contains("""'Deploy/query-store.sql' (BeforeDeployFolder: "Deploy", AfterDeployFolder: "deploy").""", ex.Message);
	}

	[Fact]
	public async Task Message_names_the_first_files_by_relative_path()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = ""
		});

		mockSet.ScriptSource.AddFiles("Tests/f.sql", "Tests/e.sql", "Tests/D.sql", "Tests/c.sql", "Tests/b.sql", "Tests/a.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesInSeveralProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesInSeveralProjectFoldersException>(project.GetTestsAsync);

		Assert.Equal(6, ex.Count);
		Assert.Equal(["Tests/a.sql", "Tests/b.sql", "Tests/c.sql", "Tests/D.sql", "Tests/e.sql"], ex.RelativePaths);
		Assert.StartsWith("6 file(s) ", ex.Message);
		Assert.Contains("""'Tests/e.sql' (CodeFolder: "", TestsFolder: "Tests"), ...""", ex.Message);
	}
}
