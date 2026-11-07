namespace DbLive.Tests.Project;

public class PathsDifferingOnlyByCaseTests
{
	[Fact]
	public async Task GetCodeGroups_throws_when_code_files_differ_only_by_case()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Code/dbo/GetUser.sql",
			"Code/dbo/getuser.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		ScriptPathsDifferOnlyByCaseException ex = await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(project.GetCodeGroupsAsync);

		// assert
		Assert.Equal(["Code/dbo/GetUser.sql", "Code/dbo/getuser.sql"], ex.RelativePaths);
		Assert.Contains("'Code/dbo/GetUser.sql'", ex.Message);
		Assert.Contains("'Code/dbo/getuser.sql'", ex.Message);
	}

	[Fact]
	public async Task GetTests_throws_when_test_folders_differ_only_by_case()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Tests/Orders/select-order.sql",
			"Tests/orders/select-order.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		ScriptPathsDifferOnlyByCaseException ex = await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(project.GetTestsAsync);

		// assert
		Assert.Equal(["Tests/Orders/select-order.sql", "Tests/orders/select-order.sql"], ex.RelativePaths);
	}

	[Fact]
	public async Task GetTests_throws_when_setup_files_differ_only_by_case()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Tests/Orders/select-order.sql",
			"Tests/Orders/setup.test.sql",
			"Tests/Orders/Setup.test.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		ScriptPathsDifferOnlyByCaseException ex = await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(project.GetTestsAsync);

		// assert
		Assert.Equal(["Tests/Orders/setup.test.sql", "Tests/Orders/Setup.test.sql"], ex.RelativePaths);
	}

	[Theory]
	[InlineData(ProjectFolder.BeforeDeploy, "BeforeDeploy")]
	[InlineData(ProjectFolder.AfterDeploy, "AfterDeploy")]
	public async Task GetFolderItems_throws_when_files_differ_only_by_case(ProjectFolder projectFolder, string folder)
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			$"{folder}/grants.sql",
			$"{folder}/Grants.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		ScriptPathsDifferOnlyByCaseException ex = await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(() => project.GetFolderItemsAsync(projectFolder));

		// assert
		Assert.Equal([$"{folder}/grants.sql", $"{folder}/Grants.sql"], ex.RelativePaths);
	}

	[Fact]
	public async Task GetMigrations_throws_when_migrations_differ_only_by_case()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Migrations/001.migration.sql",
			"Migrations/001.Migration.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act & assert: the paths are named, not only the duplicate version.
		await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(project.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrations_throws_when_files_of_another_folder_differ_only_by_case()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Migrations/001.migration.sql",
			"Code/dbo/GetUser.sql",
			"Code/dbo/getuser.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act & assert: migrations are read first, so the deployment stops before anything is deployed.
		ScriptPathsDifferOnlyByCaseException ex = await Assert.ThrowsAsync<ScriptPathsDifferOnlyByCaseException>(project.GetMigrationsAsync);

		Assert.Equal(["Code/dbo/GetUser.sql", "Code/dbo/getuser.sql"], ex.RelativePaths);
	}
}
