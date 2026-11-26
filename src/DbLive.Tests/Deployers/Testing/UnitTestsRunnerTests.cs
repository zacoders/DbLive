using Xunit.Sdk;

namespace DbLive.Tests.Deployers.Testing;

public class UnitTestsRunnerTests
{
	[Fact]
	public async Task RunTest_Simple_Success()
	{
		// Arrange
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		TestItem testItem1 = new()
		{
			Name = "test1",
			FileData = GetFileData("/tests/test1.sql")
		};

		TestItem testItem2 = new()
		{
			Name = "test2",
			FileData = GetFileData("/tests/test1.sql"),
			SetupFileData = GetFileData("/tests/setup.test.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		// Act
		await runner.RunAllTestsAsync();

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();
		await mockSet.UnitTestItemRunner.Received(2).RunTestAsync(Arg.Any<TestItem>());
	}


	[Fact]
	public async Task RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		TestItem testItem1 = new()
		{
			Name = "test1.sql",
			FileData = GetFileData("Tests/test1.sql")
		};

		TestItem testItem2 = new()
		{
			Name = "test2.sql",
			FileData = GetFileData("Tests/test2.sql"),
			SetupFileData = GetFileData("Tests/setup.test.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		// Act
		await runner.RunAllTestsAsync();

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();

		mockSet.Logger.Received(1).Information("PASSED: {filePath}", "Tests/test1.sql");
		mockSet.Logger.Received(1).Information("PASSED: {filePath}", "Tests/test2.sql");

		mockSet.Logger.Received()
			.Information(
				Arg.Is("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}."),
				Arg.Is(2), // passed
				Arg.Is(0)  // failed
			);
	}


	[Fact]
	public async Task RunTest_OneTestFailed_RunsAllTests_AndLogsEachResult()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForTestsRun = 1
		});

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		TestItem testItem1 = new()
		{
			Name = "test1.sql",
			FileData = GetFileData("Tests/test1.sql")
		};

		TestItem testItem2 = new()
		{
			Name = "test2.sql",
			FileData = GetFileData("Tests/test2.sql"),
			SetupFileData = GetFileData("Tests/setup.test.sql")
		};

		TestItem testItem3 = new()
		{
			Name = "test3.sql",
			FileData = GetFileData("Tests/test3.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([testItem1, testItem2, testItem3]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(call =>
			{
				TestItem item = call.Arg<TestItem>();
				return Task.FromResult(new TestRunResult
				{
					IsSuccess = item != testItem2,
					ErrorMessage = item == testItem2 ? "failed" : null
				});
			});

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(runner.RunAllTestsAsync);

		Assert.Equal(1, ex.FailedCount);
		Assert.Equal(3, ex.TotalCount);
		Assert.Equal(testItem2.FileData.RelativePath, Assert.Single(ex.FailedTests));
		Assert.Equal("Unit test run failed. Failed test count 1 of 3: 'Tests/test2.sql'.", ex.Message);

		await mockSet.UnitTestItemRunner.Received(3).RunTestAsync(Arg.Any<TestItem>());

		// the log is where a failure is: the tests write nothing to the dblive tables.
		mockSet.Logger.Received(1).Information("PASSED: {filePath}", "Tests/test1.sql");
		mockSet.Logger.Received(1).Error((Exception?)null, "FAILED: {filePath}. Error Message: {errorMessage}", "Tests/test2.sql", "failed");
		mockSet.Logger.Received(1).Information("PASSED: {filePath}", "Tests/test3.sql");

		mockSet.Logger.Received()
			.Information(
				Arg.Is("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}."),
				Arg.Is(2), // passed
				Arg.Is(1)  // failed
			);
	}

	[Fact]
	public async Task RunTest_Failed_Test_Keeps_The_Real_Casing_Of_Its_Path()
	{
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		// the exception shows the path of the file, not the lower-case path that DbLive stores.
		TestItem testItem = new()
		{
			Name = "Select-Order.sql",
			FileData = GetFileData("Tests/Orders/Select-Order.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([testItem]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = false, ErrorMessage = "failed" });

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(runner.RunAllTestsAsync);

		Assert.Equal(["Tests/Orders/Select-Order.sql"], ex.FailedTests);
	}

	[Fact]
	public async Task RunTest_Tests_With_The_Same_File_Name_Are_Told_Apart_By_Their_Paths()
	{
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		TestItem orders = new() { Name = "select.sql", FileData = GetFileData("Tests/Orders/select.sql") };
		TestItem users = new() { Name = "select.sql", FileData = GetFileData("Tests/Users/select.sql") };
		TestItem products = new() { Name = "select.sql", FileData = GetFileData("Tests/Products/select.sql") };

		mockSet.DbLiveProject.GetTestsAsync().Returns([users, orders, products]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(call => Task.FromResult(
				call.Arg<TestItem>() == products
					? new TestRunResult { IsSuccess = true }
					: new TestRunResult { IsSuccess = false, ErrorMessage = "failed" }
			));

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(runner.RunAllTestsAsync);

		Assert.Equal("Unit test run failed. Failed test count 2 of 3: 'Tests/Orders/select.sql', 'Tests/Users/select.sql'.", ex.Message);

		mockSet.Logger.Received(1).Information("PASSED: {filePath}", "Tests/Products/select.sql");
		mockSet.Logger.Received(1).Error((Exception?)null, "FAILED: {filePath}. Error Message: {errorMessage}", "Tests/Orders/select.sql", "failed");
		mockSet.Logger.Received(1).Error((Exception?)null, "FAILED: {filePath}. Error Message: {errorMessage}", "Tests/Users/select.sql", "failed");
	}

	[Fact]
	public async Task RunTest_Message_Names_The_First_Ten_Failed_Tests()
	{
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		// t01 ... t12, given in reverse: the message and FailedTests are sorted.
		TestItem[] tests = [.. Enumerable.Range(1, 12).Reverse().Select(i => new TestItem
		{
			Name = $"t{i:00}.sql",
			FileData = GetFileData($"Tests/t{i:00}.sql")
		})];

		mockSet.DbLiveProject.GetTestsAsync().Returns(tests);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = false, ErrorMessage = "failed" });

		UnitTestsFailedException ex = await Assert.ThrowsAsync<UnitTestsFailedException>(runner.RunAllTestsAsync);

		Assert.Equal(
			"Unit test run failed. Failed test count 12 of 12: "
			+ "'Tests/t01.sql', 'Tests/t02.sql', 'Tests/t03.sql', 'Tests/t04.sql', 'Tests/t05.sql', "
			+ "'Tests/t06.sql', 'Tests/t07.sql', 'Tests/t08.sql', 'Tests/t09.sql', 'Tests/t10.sql' and 2 more.",
			ex.Message
		);
		Assert.Equal(12, ex.FailedTests.Count);
		Assert.Equal("Tests/t12.sql", ex.FailedTests[^1]);
	}

	[Fact]
	public async Task RunTest_Logs_Under_Its_Own_Context()
	{
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		await runner.RunAllTestsAsync();

		_ = mockSet.Logger.Received().ForContext(typeof(UnitTestsRunner));
		_ = mockSet.Logger.DidNotReceive().ForContext(typeof(UnitTestItemRunner));
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