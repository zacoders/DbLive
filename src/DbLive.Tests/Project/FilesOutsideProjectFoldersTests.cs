namespace DbLive.Tests.Project;

public class FilesOutsideProjectFoldersTests
{
	[Fact]
	public async Task Folder_outside_the_project_fails_every_read()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Migrations/001.migration.sql",
			"Code/GetUser.sql",
			"PostDeployment/query-store.sql",
			"PostDeployment/documentation.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// the deployment reads migrations first, so it stops before anything is deployed.
		FilesOutsideProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetMigrationsAsync);

		Assert.Equal(2, ex.Count);
		Assert.Equal(["PostDeployment/documentation.sql", "PostDeployment/query-store.sql"], ex.RelativePaths);
		Assert.Contains("'PostDeployment/documentation.sql', 'PostDeployment/query-store.sql'.", ex.Message);
		Assert.Contains("the folders Migrations, Code, Tests, BeforeDeploy, AfterDeploy.", ex.Message);

		_ = await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetCodeGroupsAsync);
		_ = await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetCodeBreakingItemsAsync);
		_ = await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetTestsAsync);
		_ = await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(() => project.GetFolderItemsAsync(ProjectFolder.AfterDeploy));
	}

	[Theory]
	[InlineData("deploy.sql")]
	[InlineData("README.md")]
	[InlineData("setting.json")]
	[InlineData("CodeX/GetUser.sql")]
	[InlineData("Old/Code/GetUser.sql")]
	public async Task File_outside_the_project_folders_fails(string relativePath)
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles("Code/GetUser.sql", relativePath);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesOutsideProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetCodeGroupsAsync);

		Assert.Equal([relativePath], ex.RelativePaths);
	}

	[Fact]
	public async Task Settings_file_and_hidden_files_are_not_outside()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Settings.JSON",
			".DS_Store",
			".gitkeep",
			".vs/DbLive/v17/.suo",
			"Code/.DS_Store",
			"Old/.hidden/GetUser.sql",
			"code/dbo/GetUser.sql",
			"MIGRATIONS/001.migration.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		_ = Assert.Single(await project.GetMigrationsAsync());
		_ = Assert.Single(await project.GetCodeGroupsAsync());
	}

	[Fact]
	public async Task Message_names_the_first_files_by_relative_path()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles("Old/g.sql", "Old/f.sql", "Old/e.sql", "Old/D.sql", "Old/c.sql", "Old/b.sql", "Old/a.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesOutsideProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetMigrationsAsync);

		Assert.Equal(7, ex.Count);
		Assert.Equal(["Old/a.sql", "Old/b.sql", "Old/c.sql", "Old/D.sql", "Old/e.sql"], ex.RelativePaths);
		Assert.StartsWith("7 file(s) ", ex.Message);
		Assert.Contains("'Old/e.sql', ...", ex.Message);
	}

	[Fact]
	public async Task Folders_are_the_names_from_settings()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			MigrationsFolder = "db/Migrations",
			CodeFolder = @"db\Code",
			TestsFolder = "/db/Tests/",
			AfterDeployFolder = "PostDeployment"
		});

		mockSet.ScriptSource.AddFiles(
			"db/Migrations/001.migration.sql",
			"db/Code/GetUser.sql",
			"db/Tests/GetUser.sql",
			"PostDeployment/query-store.sql",
			// the default folders in the root and other files of a nested folder are outside the project.
			"Code/GetUser.sql",
			"db/notes.md",
			"AfterDeploy/query-store.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		FilesOutsideProjectFoldersException ex =
			await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(project.GetMigrationsAsync);

		Assert.Equal(["AfterDeploy/query-store.sql", "Code/GetUser.sql", "db/notes.md"], ex.RelativePaths);
		Assert.Contains("the folders db/Migrations, db/Code, db/Tests, BeforeDeploy, PostDeployment.", ex.Message);
	}
}
