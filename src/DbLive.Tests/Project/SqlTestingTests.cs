
namespace DbLive.Tests.Project;

public class SqlTestingTests
{
	[Fact]
	public async Task GetTests_collects_tests_from_tests_and_code_folders()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(
			new DbLiveSettings
			{
				TestsFolder = "Tests",
				CodeFolder = "Code",
				TestFilePatterns = ["*.test.sql"]
			}
		);

		mockSet.ScriptSource.AddFiles(
			"Tests/Orders/orders.sql",
			"Tests/readme.md",
			"Code/Users/users.test.sql",
			"Code/Users/users.sql",
			"Migrations/001.migration.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		IReadOnlyCollection<TestItem> tests = await project.GetTestsAsync();

		// assert
		Assert.Equal(2, tests.Count);

		Assert.Contains(tests, t => t.Name == "orders.sql" && t.FileData.RelativePath == "Tests/Orders/orders.sql");
		Assert.Contains(tests, t => t.Name == "users.test.sql" && t.FileData.RelativePath == "Code/Users/users.test.sql");
	}

	[Fact]
	public async Task GetTests_tests_folder_does_not_exist_returns_empty()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("Migrations/001.migration.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		IReadOnlyCollection<TestItem> tests = await project.GetTestsAsync();

		// assert
		Assert.Empty(tests);
	}

	[Fact]
	public async Task GetTests_excludes_setup_file_from_test_files()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Tests/test2.sql",
			"Tests/setup.test.sql",
			"Tests/test3.sql",
			"Tests/test1.sql",
			"Code/setup.test.sql"
		);

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		IReadOnlyCollection<TestItem> tests = await project.GetTestsAsync();

		// assert
		Assert.Equal(["test1.sql", "test2.sql", "test3.sql"], tests.Select(t => t.Name));
	}

	[Fact]
	public async Task GetTests_setup_file_is_attached_to_the_tests_of_its_folder()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Tests/Orders/Setup.Test.sql", "setup")
			.AddFile("Tests/Orders/a.sql")
			.AddFile("Tests/Orders/b.sql")
			.AddFile("Tests/Orders/Sub/c.sql")
			.AddFile("Tests/d.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		IReadOnlyCollection<TestItem> tests = await project.GetTestsAsync();

		// assert
		Assert.Equal(4, tests.Count);

		TestItem[] withSetup = [.. tests.Where(t => t.SetupFileData is not null)];
		Assert.Equal(["a.sql", "b.sql"], withSetup.Select(t => t.Name));
		Assert.All(withSetup, t => Assert.Equal("setup", t.SetupFileData!.Content));
		Assert.All(withSetup, t => Assert.Equal("Tests/Orders/Setup.Test.sql", t.SetupFileData!.RelativePath));

		// read once for the folder.
		Assert.Same(withSetup[0].SetupFileData, withSetup[1].SetupFileData);
	}

	[Fact]
	public async Task GetTests_setup_file_in_code_folder()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Code/dbo/setup.test.sql", "setup")
			.AddFile("Code/dbo/GetUser.sql")
			.AddFile("Code/dbo/GetUser.test.sql");

		DbLiveProject project = mockSet.CreateUsingMocks<DbLiveProject>();

		// act
		TestItem test = Assert.Single(await project.GetTestsAsync());

		// assert
		Assert.Equal("Code/dbo/GetUser.test.sql", test.FileData.RelativePath);
		Assert.Equal("Code/dbo/setup.test.sql", test.SetupFileData?.RelativePath);
	}
}
