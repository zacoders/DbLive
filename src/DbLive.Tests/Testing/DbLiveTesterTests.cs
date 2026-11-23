namespace DbLive.Tests.Testing;

public class DbLiveTesterTests
{
	[Fact]
	public async Task RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			new TestItem { Name = "first", FileData = GetFileData("/test/first.sql") },
			new TestItem { Name = "second", FileData = GetFileData("/test/second.sql") },
			new TestItem { Name = "third", FileData = GetFileData("/test/third.sql") }
		]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = true });

		Action<string> writeLine = Console.WriteLine;

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		// Act
		await tester.RunTestAsync(writeLine, "/test/first.sql");

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();
	}


	[Fact]
	public async Task RunTest_WithInit()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			new TestItem
			{
				Name = "first",
				FileData = GetFileData("/test/first.sql"),
				SetupFileData = GetFileData("/test/setup.test.sql")
			},
			new TestItem { Name = "second", FileData = GetFileData("/test/second.sql") },
			new TestItem { Name = "third", FileData = GetFileData("/test/third.sql") }
		]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = true });

		Action<string> writeLine = Console.WriteLine;

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		// Act
		await tester.RunTestAsync(writeLine, "/test/first.sql");

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();
	}

	[Theory]
	[InlineData("Tests/Orders/Select-Order.sql")]
	[InlineData("tests/orders/select-order.SQL")]
	[InlineData("TESTS/ORDERS/SELECT-ORDER.SQL")]
	[InlineData(@"Tests\Orders\Select-Order.sql")]
	[InlineData(@"tests\orders/select-order.sql")]
	public async Task RunTest_finds_the_test_ignoring_case_and_with_backslashes(string relativePath)
	{
		MockSet mockSet = new();
		TestItem selectOrder = new() { Name = "Select-Order.sql", FileData = GetFileData("Tests/Orders/Select-Order.sql") };
		mockSet.DbLiveProject.GetTestsAsync().Returns([
			new TestItem { Name = "Select-Order.sql", FileData = GetFileData("Tests/Select-Order.sql") },
			selectOrder,
			new TestItem { Name = "Select-Order.sql", FileData = GetFileData("Tests/Users/Select-Order.sql") }
		]);
		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = true });

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		TestRunResult result = await tester.RunTestAsync(_ => { }, relativePath);

		Assert.True(result.IsSuccess);
		await mockSet.UnitTestItemRunner.Received(1).RunTestAsync(Arg.Any<TestItem>());
		await mockSet.UnitTestItemRunner.Received(1).RunTestAsync(selectOrder);
	}

	[Theory]
	[InlineData("Tests/Orders/missing.sql")]
	[InlineData("Scripts/Tests/Orders/Select-Order.sql")]
	[InlineData("Code/dbo/GetOrder.sql")]
	[InlineData(@"Tests\Orders\missing.sql")]
	public async Task RunTest_throws_when_the_path_is_not_a_test_of_the_project(string relativePath)
	{
		MockSet mockSet = new();
		mockSet.DbLiveProject.GetTestsAsync().Returns([
			new TestItem { Name = "Select-Order.sql", FileData = GetFileData("Tests/Orders/Select-Order.sql") }
		]);

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		KeyNotFoundException ex = await Assert.ThrowsAsync<KeyNotFoundException>(
			() => tester.RunTestAsync(_ => { }, relativePath)
		);

		Assert.Equal(
			$"Test '{relativePath}' not found in the project. Pass the path of the test file relative to the scripts folder, for example 'Tests/Orders/select-order.test.sql'.",
			ex.Message
		);
		await mockSet.UnitTestItemRunner.DidNotReceiveWithAnyArgs().RunTestAsync(default!);
	}

	[Fact]
	public async Task RunAllTests_checks_the_database_then_runs_the_tests()
	{
		MockSet mockSet = new();
		DbLiveSettings settings = new() { ProjectId = "my-app" };
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(settings);
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		await tester.RunAllTestsAsync();

		Received.InOrder(() =>
		{
			_ = mockSet.DbLiveDA.CheckDatabaseExistsAsync();
			_ = mockSet.DbLiveDA.DbLiveInstalledAsync();
			_ = mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync();
			_ = mockSet.ProjectIdValidator.ValidateBeforeTestsAsync();
			_ = mockSet.TransactionSettingsValidator.ValidateAsync(settings);
			_ = mockSet.UnitTestsRunner.RunAllTestsAsync();
		});
		// the tests only compare the ProjectId: binding it is the job of a deployment.
		await mockSet.ProjectIdValidator.DidNotReceive().ValidateAsync();
		// the requirement is only checked: a deployment writes it.
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetMinDbLiveLibVersionAsync(default!);
	}

	[Fact]
	public async Task RunAllTests_throws_when_the_database_does_not_exist()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.CheckDatabaseExistsAsync()
			.Returns(Task.FromException(new DatabaseNotFoundException("MyDb", "localhost", new Exception("cannot open"))));

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		_ = await Assert.ThrowsAsync<DatabaseNotFoundException>(tester.RunAllTestsAsync);

		_ = mockSet.DbLiveDA.DidNotReceive().DbLiveInstalledAsync();
		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
	}

	[Fact]
	public async Task RunAllTests_throws_when_the_database_has_no_DbLive_tables()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		DatabaseNotDeployedException ex = await Assert.ThrowsAsync<DatabaseNotDeployedException>(tester.RunAllTestsAsync);

		Assert.Equal(
			"The database has no DbLive tables: the project was not deployed to it. Deploy the project before running the SQL tests.",
			ex.Message
		);
		await mockSet.ProjectIdValidator.DidNotReceive().ValidateBeforeTestsAsync();
		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
	}

	[Fact]
	public async Task RunAllTests_throws_when_the_database_requires_a_newer_DbLive()
	{
		Version newerVersion = new(DbLiveCompatibility.MinCompatibleVersion.Major + 1, 0, 0);

		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetMinDbLiveLibVersionAsync().Returns(newerVersion);

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		DatabaseRequiresNewerDbLiveException ex = await Assert.ThrowsAsync<DatabaseRequiresNewerDbLiveException>(tester.RunAllTestsAsync);

		Assert.Equal(newerVersion, ex.RequiredVersion);
		await mockSet.ProjectIdValidator.DidNotReceive().ValidateBeforeTestsAsync();
		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs().SetMinDbLiveLibVersionAsync(default!);
	}

	[Fact]
	public async Task RunAllTests_does_not_run_the_tests_when_the_ProjectId_check_fails()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.ProjectIdValidator.ValidateBeforeTestsAsync()
			.Returns(Task.FromException(new ProjectIdMismatchException("mismatch")));

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		_ = await Assert.ThrowsAsync<ProjectIdMismatchException>(tester.RunAllTestsAsync);

		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
	}

	[Fact]
	public async Task RunAllTests_does_not_run_the_tests_when_the_transaction_settings_check_fails()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.TransactionSettingsValidator.ValidateAsync(Arg.Any<DbLiveSettings>())
			.Returns(Task.FromException(new InvalidOperationException("bad isolation level")));

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		_ = await Assert.ThrowsAsync<InvalidOperationException>(tester.RunAllTestsAsync);

		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
	}

	[Fact]
	public async Task RunAllTests_passes_on_the_failure_of_the_tests()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.UnitTestsRunner.RunAllTestsAsync()
			.Returns(Task.FromException(new UnitTestsFailedException(1, 2, ["Tests/t1.sql"])));

		DbLiveTester tester = mockSet.CreateUsingMocks<DbLiveTester>();

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(tester.RunAllTestsAsync);

		Assert.Equal(["Tests/t1.sql"], ex.FailedTests);
	}

	private static FileData GetFileData(string relativePath)
	{
		return new FileData
		{
			Content = $"-- unit test mock content {Guid.NewGuid()}",
			RelativePath = relativePath
		};
	}
}