namespace DbLive.Tests.Project;

public class CodeItemsTests
{
	[Fact]
	public async Task GetCodeItems()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Code/item1.sql", "code item content")
			.AddFile("Code/item2.sql")
			.AddFile("Code/sub/item1.sql")
			.AddFile("Code/sub/item2.sql")
			.AddFile("Migrations/001.migration.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		CodeGroup group = Assert.Single(codeGroups);
		Assert.Equal("Code", group.Path);
		Assert.Equal(4, group.CodeItems.Count);

		CodeItem item = group.CodeItems.First();
		Assert.Equal("item1.sql", item.Name);
		Assert.Equal("Code/item1.sql", item.FileData.RelativePath);
		Assert.Equal("code item content", item.FileData.Content);
	}

	[Fact]
	public async Task GetCodeItems_SortedByRelativePath_OrdinalIgnoreCase()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Code/sub/c.sql",
			"Code/z.sql",
			"Code/c.sql",
			"Code/a-b/d.sql",
			"Code/A.sql",
			"Code/a_b.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		CodeGroup group = Assert.Single(await sqlProject.GetCodeGroupsAsync());

		// the same on every OS and in every culture: '-' < '.' < '/' < letters < '_'.
		Assert.Equal(
			["Code/a-b/d.sql", "Code/A.sql", "Code/a_b.sql", "Code/c.sql", "Code/sub/c.sql", "Code/z.sql"],
			group.CodeItems.Select(i => i.FileData.RelativePath)
		);
	}

	[Fact]
	public async Task GetCodeItems_With_CodeSubFoldersDeploymentOrder()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = ["sub2", "sub1"]
		});

		mockSet.ScriptSource.AddFiles(
			"Code/item1.sql",
			"Code/item2.sql",
			"Code/sub1/item1.sql",
			"Code/sub1/item2.sql",
			"Code/sub1/item3.sql",
			"Code/sub2/item1.sql",
			"Code/sub10/item1.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.Equal(3, codeGroups.Count);

		Assert.Equal("Code/sub2", codeGroups[0].Path);
		Assert.Single(codeGroups[0].CodeItems);

		Assert.Equal("Code/sub1", codeGroups[1].Path);
		Assert.Equal(3, codeGroups[1].CodeItems.Count);

		// other items, sub10 is not in sub1
		Assert.Equal("Code", codeGroups[2].Path);
		Assert.Equal(
			["Code/item1.sql", "Code/item2.sql", "Code/sub10/item1.sql"],
			codeGroups[2].CodeItems.Select(i => i.FileData.RelativePath)
		);
	}

	[Theory]
	[InlineData(@"dbo\functions")]
	[InlineData("dbo/functions")]
	[InlineData("dbo/functions/")]
	[InlineData(@"\dbo\functions\")]
	[InlineData("DBO/Functions")]
	public async Task GetCodeItems_With_Nested_CodeSubFoldersDeploymentOrder(string subFolder)
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = [subFolder]
		});

		mockSet.ScriptSource.AddFiles(
			"Code/item1.sql",
			"Code/dbo/table1.sql",
			"Code/dbo/functions/function1.sql",
			"Code/dbo/functions/function2.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.Equal(2, codeGroups.Count);

		Assert.Equal(
			["Code/dbo/functions/function1.sql", "Code/dbo/functions/function2.sql"],
			codeGroups[0].CodeItems.Select(i => i.FileData.RelativePath)
		);

		// other items
		Assert.Equal(2, codeGroups[1].CodeItems.Count);

		mockSet.Logger.DidNotReceive().Warning(Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	public async Task GetCodeItems_Warns_When_CodeSubFolder_Matches_No_Files()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = ["missing"]
		});

		mockSet.ScriptSource.AddFile("Code/item1.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.Equal(2, codeGroups.Count);
		Assert.Empty(codeGroups[0].CodeItems);
		Assert.Single(codeGroups[1].CodeItems);

		mockSet.Logger.Received(1).Warning(Arg.Any<string>(), Arg.Is("missing"));
	}

	[Fact]
	public async Task GetCodeItems_Excludes_TestSetupFile()
	{
		MockSet mockSet = new();

		// custom patterns do not match the setup file, it must be excluded anyway.
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TestFilePatterns = ["*.spec.sql"]
		});

		mockSet.ScriptSource.AddFiles(
			"Code/item1.sql",
			"Code/item1.spec.sql",
			"Code/Setup.Test.sql",
			"Code/notes.txt",
			"Code/item2.sqlx"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		CodeGroup group = Assert.Single(codeGroups);
		CodeItem item = Assert.Single(group.CodeItems);
		Assert.Equal("Code/item1.sql", item.FileData.RelativePath);
	}

	[Fact]
	public async Task GetCodeItems_Matches_Files_Ignoring_Case()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeFolder = "code"
		});

		mockSet.ScriptSource.AddFiles("Code/dbo/V1.SQL", "Code/dbo/v1.Test.Sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		CodeGroup group = Assert.Single(await sqlProject.GetCodeGroupsAsync());

		// the real names of the files and folders, not the casing from the settings.
		CodeItem item = Assert.Single(group.CodeItems);
		Assert.Equal("Code/dbo/V1.SQL", item.FileData.RelativePath);
	}

	[Fact]
	public async Task GetCodeItems_No_Code_No_Groups()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = ["sub1"]
		});

		mockSet.ScriptSource.AddFiles("Migrations/001.migration.sql", "Code/a.test.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		Assert.Empty(await sqlProject.GetCodeGroupsAsync());

		mockSet.Logger.DidNotReceive().Warning(Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	public async Task GetCodeItems_FailedRead_ReadAgain()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Code/first.sql", "first")
			.AddFile("Code/second.sql", "second")
			.FailNextRead("Code/second.sql", new IOException("The file is being used by another process."));

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// a file locked for a moment: the first deployment fails.
		_ = await Assert.ThrowsAsync<IOException>(sqlProject.GetCodeGroupsAsync);

		// the next deployment from the same builder reads the file again.
		CodeGroup group = Assert.Single(await sqlProject.GetCodeGroupsAsync());
		Assert.Equal(["first", "second"], group.CodeItems.Select(i => i.FileData.Content));

		// a successful read is kept.
		_ = await sqlProject.GetCodeGroupsAsync();
		Assert.Equal(1, mockSet.ScriptSource.ReadCount("Code/first.sql"));
		Assert.Equal(2, mockSet.ScriptSource.ReadCount("Code/second.sql"));
	}
}
