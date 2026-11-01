namespace DbLive.Tests.Project;

public class CodeBreakingItemsTests
{
	[Fact]
	public async Task Breaking_scripts_of_the_Code_folder_are_found_by_full_and_short_names()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Code/dbo/GetUser.breaking.sql", "drop proc if exists dbo.GetUser_v1")
			.AddFiles(
				"Code/dbo/GetUser.sql",
				"Code/dbo/breaking.sql",
				"Code/sales/GetOrder.b.sql",
				"Code/sales/B.SQL",
				"Code/sales/Old.Breaking.Sql",
				// not breaking names
				"Code/sales/breaking-news.sql",
				"Code/sales/b.sql.txt",
				// breaking scripts of a migration, a test and an AfterDeploy script, not of the Code folder
				"Migrations/001.migration.sql",
				"Migrations/001.b.sql",
				"Tests/t.b.sql",
				"AfterDeploy/b.sql"
			);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		IReadOnlyList<GenericItem> items = await project.GetCodeBreakingItemsAsync();

		// sorted by relative path, ordinal, ignoring case.
		Assert.Equal(
			[
				"Code/dbo/breaking.sql",
				"Code/dbo/GetUser.breaking.sql",
				"Code/sales/B.SQL",
				"Code/sales/GetOrder.b.sql",
				"Code/sales/Old.Breaking.Sql"
			],
			items.Select(i => i.FileData.RelativePath)
		);

		GenericItem item = items[1];
		Assert.Equal("GetUser.breaking.sql", item.Name);
		Assert.Equal("drop proc if exists dbo.GetUser_v1", item.FileData.Content);
	}

	[Fact]
	public async Task Breaking_scripts_are_not_code_items_and_not_tests()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Code/GetUser.sql",
			"Code/GetUser.test.sql",
			"Code/GetUser.breaking.sql",
			"Code/GetUser.b.sql",
			"Code/breaking.sql",
			"Code/b.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		CodeGroup group = Assert.Single(await project.GetCodeGroupsAsync());
		Assert.Equal(["Code/GetUser.sql"], group.CodeItems.Select(i => i.FileData.RelativePath));

		TestItem test = Assert.Single(await project.GetTestsAsync());
		Assert.Equal("Code/GetUser.test.sql", test.FileData.RelativePath);

		Assert.Equal(4, (await project.GetCodeBreakingItemsAsync()).Count);
	}

	[Fact]
	public async Task Breaking_script_without_a_code_file_is_valid()
	{
		MockSet mockSet = new();

		// a procedure retired together with its file.
		mockSet.ScriptSource.AddFiles("Code/dbo/GetUser.sql", "Code/dbo/GetOrder.breaking.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem item = Assert.Single(await project.GetCodeBreakingItemsAsync());
		Assert.Equal("Code/dbo/GetOrder.breaking.sql", item.FileData.RelativePath);
	}

	[Fact]
	public async Task Breaking_scripts_are_found_in_the_code_folder_from_the_settings()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = @"db\Code"
		});

		mockSet.ScriptSource.AddFiles("db/Code/GetUser.sql", "db/Code/GetUser.b.sql", "Code/GetOrder.b.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem item = Assert.Single(await project.GetCodeBreakingItemsAsync());
		Assert.Equal("db/Code/GetUser.b.sql", item.FileData.RelativePath);
	}

	[Fact]
	public async Task No_breaking_scripts()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles("Code/GetUser.sql", "Migrations/001.migration.sql", "Migrations/001.breaking.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		Assert.Empty(await project.GetCodeBreakingItemsAsync());
	}

	[Theory]
	[InlineData("Code/dbo/test.breaking.sql")]
	[InlineData("Code/dbo/t.b.sql")]
	[InlineData("Code/dbo/test.b.sql")]
	[InlineData("Code/dbo/T.Breaking.SQL")]
	public async Task Breaking_name_that_matches_a_test_pattern_fails_every_read(string relativePath)
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles("Migrations/001.migration.sql", "Code/dbo/GetUser.sql", relativePath);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// the deployment reads migrations first, so it stops before anything is deployed.
		BreakingScriptMatchesTestPatternException ex =
			await Assert.ThrowsAsync<BreakingScriptMatchesTestPatternException>(project.GetMigrationsAsync);

		Assert.Equal(relativePath, ex.RelativePath);
		Assert.Contains($"'{relativePath}'", ex.Message);

		_ = await Assert.ThrowsAsync<BreakingScriptMatchesTestPatternException>(project.GetCodeGroupsAsync);
		_ = await Assert.ThrowsAsync<BreakingScriptMatchesTestPatternException>(project.GetCodeBreakingItemsAsync);
		_ = await Assert.ThrowsAsync<BreakingScriptMatchesTestPatternException>(project.GetTestsAsync);
		_ = await Assert.ThrowsAsync<BreakingScriptMatchesTestPatternException>(() => project.GetFolderItemsAsync(ProjectFolder.AfterDeploy));
	}

	[Fact]
	public async Task With_other_test_patterns_a_breaking_name_is_a_breaking_script()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TestFilePatterns = ["*.spec.sql"]
		});

		mockSet.ScriptSource.AddFiles("Code/test.breaking.sql", "Code/GetUser.spec.sql", "Tests/t.b.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		GenericItem item = Assert.Single(await project.GetCodeBreakingItemsAsync());
		Assert.Equal("Code/test.breaking.sql", item.FileData.RelativePath);

		Assert.Equal(
			["Tests/t.b.sql", "Code/GetUser.spec.sql"],
			(await project.GetTestsAsync()).Select(t => t.FileData.RelativePath)
		);
	}
}
