namespace DbLive.Tests.Project;

public class CodeItemsTests
{
	[Fact]
	public async Task GetCodeItems()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string codePath = projectPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith(@"item1.sql"),
			codePath.CombineWith(@"item2.sql"),
			codePath.CombineWith(@"sub").CombineWith("item1.sql"),
			codePath.CombineWith(@"sub").CombineWith("item2.sql")
		]);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.NotNull(codeGroups);
		Assert.Single(codeGroups);
		Assert.Equal(4, codeGroups[0].CodeItems.Count);
	}


	[Fact]
	public async Task GetCodeItems_With_CodeSubFoldersDeploymentOrder()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = ["sub2", "sub1"]
		});

		string projekt = @"C:/DB";
		string projektPath = projekt;
		mockSet.ProjectPath.Path.Returns(projektPath);

		string codePath = projektPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith(@"item1.sql"),
			codePath.CombineWith(@"item2.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item1.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item2.sql"),
			codePath.CombineWith(@"sub1").CombineWith("item3.sql"),
			codePath.CombineWith(@"sub2").CombineWith("item1.sql")
		]);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.NotNull(codeGroups);
		Assert.Equal(3, codeGroups.Count);

		Assert.Contains("sub2", codeGroups[0].Path);
		Assert.Single(codeGroups[0].CodeItems);

		Assert.Contains("sub1", codeGroups[1].Path);
		Assert.Equal(3, codeGroups[1].CodeItems.Count);

		// other items
		Assert.Equal(2, codeGroups[2].CodeItems.Count);
	}

	[Theory]
	[InlineData(@"dbo\functions")]
	[InlineData("dbo/functions")]
	[InlineData("dbo/functions/")]
	[InlineData(@"\dbo\functions\")]
	public async Task GetCodeItems_With_Nested_CodeSubFoldersDeploymentOrder(string subFolder)
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			CodeSubFoldersDeploymentOrder = [subFolder]
		});

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string codePath = projectPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		// enumerated files always use the OS path separator.
		string functionsPath = codePath.CombineWith("dbo").CombineWith("functions");
		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith("item1.sql"),
			codePath.CombineWith("dbo").CombineWith("table1.sql"),
			functionsPath.CombineWith("function1.sql"),
			functionsPath.CombineWith("function2.sql")
		]);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.Equal(2, codeGroups.Count);

		Assert.Equal(functionsPath, codeGroups[0].Path);
		Assert.Equal(2, codeGroups[0].CodeItems.Count);

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

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string codePath = projectPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			Arg.Any<IEnumerable<string>>(),
			true
		).Returns([
			codePath.CombineWith("item1.sql")
		]);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

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

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string codePath = projectPath.CombineWith("Code");
		mockSet.FileSystem.PathExists(codePath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			codePath,
			MyArg.SequenceEqual(["*.sql"]),
			MyArg.SequenceEqual(["*.spec.sql", "setup.test.sql"]),
			true
		).Returns([
			codePath.CombineWith(@"item1.sql")
		]);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "code item content",
				RelativePath = ""
			});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var codeGroups = (await sqlProject.GetCodeGroupsAsync()).ToList();

		Assert.Single(codeGroups);
		Assert.Single(codeGroups[0].CodeItems);
	}
}