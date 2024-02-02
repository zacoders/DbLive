namespace EasyFlow.Tests.Project;

public class SqlTestingTests
{
	[Fact]
	public void GetTests()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<IEnumerable<string>>(), "*", SearchOption.AllDirectories)
			.Returns([
				@"C:\DB\Tests\",
				@"C:\DB\Tests\Orders",
				@"C:\DB\Tests\Users"
			]);

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var tests = sqlProject.GetTests();

		Assert.NotNull(tests);
	}

	[Fact]
	public void GetFolderTests()
	{
		var mockSet = new MockSet();

		string folderPath = @"C:\DB\Tests\";

		mockSet.FileSystem.EnumerateFiles(folderPath, Arg.Any<IEnumerable<string>>(), subfolders: false)
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

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var tests = sqlProject.GetFolderTests(folderPath);

		Assert.NotNull(tests);
		Assert.Equal(2, tests.Count);
		foreach (var test in tests)
		{
			Assert.Null(test.InitFileData);
		}
	}


	[Fact]
	public void GetFolderTests_With_InitFile()
	{
		var mockSet = new MockSet();

		string folderPath = @"C:\DB\Tests\";

		mockSet.FileSystem.EnumerateFiles(folderPath, Arg.Any<IEnumerable<string>>(), subfolders: false)
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

		string initSqlFilePath = @"C:\DB\Tests\init.sql";
		mockSet.FileSystem.FileExists(initSqlFilePath).Returns(true);
		mockSet.FileSystem.ReadFileData(initSqlFilePath, Arg.Any<string>())
			.Returns(new FileData
			{
				FilePath = initSqlFilePath,
				Content = "init content",
				RelativePath = ""
			});

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var tests = sqlProject.GetFolderTests(folderPath);

		Assert.NotNull(tests);
		Assert.Equal(2, tests.Count);
		foreach (var test in tests)
		{
			Assert.NotNull(test.InitFileData);
		}
	}
}