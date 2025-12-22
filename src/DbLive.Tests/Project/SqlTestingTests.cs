namespace EasyFlow.Tests.Project;

public class SqlTestingTests
{
	[Fact]
	public void GetTests()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<IEnumerable<string>>(), "*", SearchOption.AllDirectories)
			.Returns([
				@"C:\DB\Tests\",
				@"C:\DB\Tests\Orders",
				@"C:\DB\Tests\Users"
			]);

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		var tests = sqlProject.GetTests();

		Assert.NotNull(tests);
	}

	[Fact]
	public void GetFolderTests()
	{
		MockSet mockSet = new();


		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		string testsFolderPath = @"C:\DB\Tests\";

		mockSet.FileSystem.PathExists(testsFolderPath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(testsFolderPath, Arg.Any<IEnumerable<string>>(), subfolders: false)
			.Returns([
				@"C:\DB\Tests\order.test.sql",
				@"C:\DB\Tests\user.test.sql"
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "test",
				RelativePath = ""
			});

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		var tests = sqlProject.GetFolderTests(testsFolderPath);

		Assert.NotNull(tests);
		Assert.Equal(2, tests.Count);
		foreach (var test in tests)
		{
			Assert.Null(test.InitFileData);
		}
	}


	[Fact]
	public void GetFolderTests_EmptyTestsFolder()
	{
		MockSet mockSet = new();

		string testsFolderPath = @"C:\DB\Tests\";

		mockSet.FileSystem.PathExists(testsFolderPath).Returns(true);

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		var tests = sqlProject.GetFolderTests(testsFolderPath);

		Assert.NotNull(tests);
		Assert.Empty(tests);
	}


	[Fact]
	public void GetFolderTests_With_InitFile()
	{
		MockSet mockSet = new();

		string projectPath = @"C:\DB";
		mockSet.ProjectPathAccessor.ProjectPath.Returns(projectPath);

		string testsFolderPath = projectPath.CombineWith("Tests");

		mockSet.FileSystem.PathExists(testsFolderPath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(testsFolderPath, Arg.Any<IEnumerable<string>>(), subfolders: false)
			.Returns([
				testsFolderPath.CombineWith("order.test.sql"),
				testsFolderPath.CombineWith("user.test.sql")
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "test",
				RelativePath = ""
			});

		string initSqlFilePath = testsFolderPath.CombineWith("init.sql");
		mockSet.FileSystem.FileExists(initSqlFilePath).Returns(true);
		mockSet.FileSystem.ReadFileData(initSqlFilePath, Arg.Any<string>())
			.Returns(new FileData
			{
				FilePath = initSqlFilePath,
				Content = "init content",
				RelativePath = ""
			});

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		var tests = sqlProject.GetFolderTests(testsFolderPath);

		Assert.NotNull(tests);
		Assert.Equal(2, tests.Count);
		foreach (var test in tests)
		{
			Assert.NotNull(test.InitFileData);
		}
	}
}