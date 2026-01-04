namespace DbLive.Tests.Project;

public class SqlTestingTests
{
	[Fact]
	public void GetTests_collects_tests_from_tests_and_code_folders()
	{
		// arrange
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		mockSet.SettingsAccessor.ProjectSettings.Returns(
			new DbLiveSettings
			{
				TestsFolder = "Tests",
				CodeFolder= "Code",
				TestFilePatterns= ["*.test.sql"]
			}
		);

		string testsRoot = projectPath.CombineWith("Tests");
		string codeRoot = projectPath.CombineWith("Code");

		mockSet.FileSystem.PathExists(testsRoot).Returns(true);
		mockSet.FileSystem.PathExists(codeRoot).Returns(true);
		mockSet.FileSystem.PathExists(testsRoot.CombineWith("Orders")).Returns(true);
		mockSet.FileSystem.PathExists(codeRoot.CombineWith("Users")).Returns(true);

		mockSet.FileSystem.EnumerateDirectories(testsRoot, "*", SearchOption.AllDirectories)
			.Returns([testsRoot.CombineWith("Orders")]);

		mockSet.FileSystem.EnumerateDirectories(codeRoot, "*", SearchOption.AllDirectories)
			.Returns([codeRoot.CombineWith("Users")]);

		mockSet.FileSystem.EnumerateFiles(
				Arg.Any<string>(),
				Arg.Any<IEnumerable<string>>(),
				Arg.Any<IEnumerable<string>>(),
				false)
			.Returns(call =>
			{
				string folder = call.ArgAt<string>(0);

				return folder switch
				{
					var f when f.Contains("Orders") => [f.CombineWith("orders.sql")],
					var f when f.Contains("Users") => [f.CombineWith("users.test.sql")],
					_ => []
				};
			});

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), projectPath)
			.Returns(call => new FileData
			{
				FilePath = call.ArgAt<string>(0),
				Content = "sql",
				RelativePath = ""
			});

		var project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		var tests = project.GetTests();

		// assert
		Assert.Equal(2, tests.Count);

		Assert.Contains(tests, t => t.Name == "orders.sql");
		Assert.Contains(tests, t => t.Name == "users.test.sql");
	}

	[Fact]
	public void GetFolderTests_folder_does_not_exist_returns_empty()
	{
		// arrange
		MockSet mockSet = new();

		string folder = @"C:/DB/Tests";
		mockSet.FileSystem.PathExists(folder).Returns(false);

		var project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		var tests = project.GetFolderTests(folder, true);

		// assert
		Assert.NotNull(tests);
		Assert.Empty(tests);
	}

	[Fact]
	public void GetFolderTests_excludes_init_sql_from_test_files()
	{
		// arrange
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string folder = projectPath.CombineWith("Tests");
		mockSet.FileSystem.PathExists(folder).Returns(true);

		mockSet.FileSystem.FileExists(folder.CombineWith("init.sql")).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
				folder,
				Arg.Any<IEnumerable<string>>(),
				Arg.Any<IEnumerable<string>>(),
				false
			).Returns([
				folder.CombineWith("test1.sql"),
				folder.CombineWith("test2.sql"),
				folder.CombineWith("test3.sql")
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), projectPath)
			.Returns(call => new FileData
			{
				FilePath = call.ArgAt<string>(0),
				Content = "sql",
				RelativePath = ""
			});

		var project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		var tests = project.GetFolderTests(folder, true);

		// assert
		Assert.Equal(3, tests.Count);
		Assert.Equal("test1.sql", tests[0].Name);
		Assert.Equal("test2.sql", tests[1].Name);
		Assert.Equal("test3.sql", tests[2].Name);
	}

	[Fact]
	public void GetFolderTests_init_sql_is_attached_to_all_tests()
	{
		// arrange
		MockSet mockSet = new();

		string projectPath = @"C:/DB";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string folder = projectPath.CombineWith("Tests");
		string initPath = folder.CombineWith("init.sql");

		mockSet.FileSystem.PathExists(folder).Returns(true);
		mockSet.FileSystem.FileExists(initPath).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			folder,
			Arg.Any<IEnumerable<string>>(),
			Arg.Any<IEnumerable<string>>(),
			false)
		.Returns([
			folder.CombineWith("a.sql"),
		folder.CombineWith("b.sql")
		]);

		mockSet.FileSystem.ReadFileData(initPath, projectPath)
			.Returns(new FileData
			{
				FilePath = initPath,
				Content = "init",
				RelativePath = ""
			});

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), projectPath)
			.Returns(call => new FileData
			{
				FilePath = call.ArgAt<string>(0),
				Content = "sql",
				RelativePath = ""
			});

		var project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		var tests = project.GetFolderTests(folder, true);

		// assert
		Assert.Equal(2, tests.Count);
		Assert.All(tests, t => Assert.NotNull(t.InitFileData));
	}

}