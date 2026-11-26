namespace DbLive.Tests.Deployers.Testing;

public class UnitTestItemRunnerTests
{
	[Fact]
	public async Task RunTest()
	{
		// Arrange
		MockSet mockSet = CreateMockSet();

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		DateTime startUtc = DateTime.UtcNow;
		DateTime endUtc = startUtc.AddSeconds(1);
		mockSet.TimeProvider.UtcNow().Returns(_ => startUtc, _ => endUtc);

		IStopWatch mockStopWatch = Substitute.For<IStopWatch>();
		mockStopWatch.ElapsedMilliseconds.Returns(999);
		mockSet.TimeProvider.StartNewStopwatch().Returns(mockStopWatch);

		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		};

		// Act
		TestRunResult result = await runner.RunTestAsync(testItem);


		// Assert
		mockSet.DbLiveDA.Received(1);
		await mockSet.DbLiveDA.Received().ExecuteQueryMultipleAsync(
			Arg.Is(testItem.FileData.Content),
			TranIsolationLevel.Serializable,
			TimeSpan.FromMinutes(1)
		);
		mockSet.UnitTestResultChecker.Received().ValidateTestResult(Arg.Any<List<SqlResult>>());

		Assert.True(result.IsSuccess);
		Assert.Equal(startUtc, result.StartedUtc);
		Assert.Equal(endUtc, result.CompletedUtc);
		Assert.Null(result.ErrorMessage);
		Assert.Equal(999, result.ExecutionTimeMs);
	}


	[Fact]
	public async Task RunTest_With_SetupFileData()
	{
		// Arrange
		MockSet mockSet = CreateMockSet();

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		DateTime startUtc = DateTime.UtcNow;
		DateTime endUtc = startUtc.AddSeconds(1);
		mockSet.TimeProvider.UtcNow().Returns(_ => startUtc, _ => endUtc);

		IStopWatch mockStopWatch = Substitute.For<IStopWatch>();
		mockStopWatch.ElapsedMilliseconds.Returns(999);
		mockSet.TimeProvider.StartNewStopwatch().Returns(mockStopWatch);

		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = GetFileData("/test/folder/setup.test.sql"),
		};

		// Act
		TestRunResult result = await runner.RunTestAsync(testItem);


		// Assert
		mockSet.DbLiveDA.Received(2);

		Received.InOrder(() =>
		{
			mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(
				Arg.Is(testItem.SetupFileData.Content),
				TranIsolationLevel.Serializable,
				TimeSpan.FromMinutes(1)
			);
			mockSet.DbLiveDA.Received().ExecuteQueryMultipleAsync(
				Arg.Is(testItem.FileData.Content),
				TranIsolationLevel.Serializable,
				TimeSpan.FromMinutes(1)
			);
		});

		Assert.True(result.IsSuccess);
		Assert.Equal(startUtc, result.StartedUtc);
		Assert.Equal(endUtc, result.CompletedUtc);
		Assert.Null(result.ErrorMessage);
		Assert.Equal(999, result.ExecutionTimeMs);
	}

	[Fact]
	public async Task RunTest_TestFailed_Exception()
	{
		// Arrange
		MockSet mockSet = CreateMockSet();

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		DateTime startUtc = DateTime.UtcNow;
		DateTime endUtc = startUtc.AddSeconds(1);
		mockSet.TimeProvider.UtcNow().Returns(_ => startUtc, _ => endUtc);

		IStopWatch mockStopWatch = Substitute.For<IStopWatch>();
		mockStopWatch.ElapsedMilliseconds.Returns(999);
		mockSet.TimeProvider.StartNewStopwatch().Returns(mockStopWatch);

		mockSet.DbLiveDA
			.When(fake => fake.ExecuteQueryMultipleAsync(Arg.Any<string>(), TranIsolationLevel.Serializable, Arg.Any<TimeSpan>()))
			.Throw<Exception>();

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		};

		// Act
		TestRunResult result = await runner.RunTestAsync(testItem);


		// Assert
		mockSet.DbLiveDA.Received(1);
		await mockSet.DbLiveDA.Received().ExecuteQueryMultipleAsync(
			Arg.Is(testItem.FileData.Content),
			TranIsolationLevel.Serializable,
			TimeSpan.FromMinutes(1)
		);

		Assert.False(result.IsSuccess);
		Assert.Equal(startUtc, result.StartedUtc);
		Assert.Equal(endUtc, result.CompletedUtc);
		Assert.Equal("Exception of type 'System.Exception' was thrown.", result.ErrorMessage);
		Assert.NotNull(result.Exception);
		Assert.Equal(999, result.ExecutionTimeMs);
	}

	[Fact]
	public async Task RunTest_UsesTestsTransactionIsolationLevelFromSettings()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TestsTransactionIsolationLevel = TranIsolationLevel.ReadCommitted,
			UnitTestItemTimeout = TimeSpan.FromMinutes(2)
		});

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		mockSet.TimeProvider.UtcNow().Returns(DateTime.UtcNow);
		IStopWatch mockStopWatch = Substitute.For<IStopWatch>();
		mockStopWatch.ElapsedMilliseconds.Returns(100);
		mockSet.TimeProvider.StartNewStopwatch().Returns(mockStopWatch);
		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		};

		_ = await runner.RunTestAsync(testItem);

		await mockSet.DbLiveDA.Received().ExecuteQueryMultipleAsync(
			Arg.Is(testItem.FileData.Content),
			TranIsolationLevel.ReadCommitted,
			TimeSpan.FromMinutes(2)
		);
	}

	[Fact]
	public async Task RunTest_TransactionEnded_FailsWithoutCheckingTheResult()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(false);
		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = GetFileData("/test/folder/setup.test.sql")
		};

		TestRunResult result = await runner.RunTestAsync(testItem);

		// one check after the test, for a transaction that ended without failing the setup file or the test.
		Received.InOrder(() =>
		{
			mockSet.DbLiveDA.ExecuteNonQueryAsync(testItem.SetupFileData.Content, Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
			mockSet.DbLiveDA.ExecuteQueryMultipleAsync(testItem.FileData.Content, Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
			mockSet.DbLiveDA.IsTransactionUsableAsync();
		});
		_ = await mockSet.DbLiveDA.Received(1).IsTransactionUsableAsync();
		_ = mockSet.UnitTestResultChecker.DidNotReceive().ValidateTestResult(Arg.Any<List<SqlResult>>());

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.ErrorMessage);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.Output);
		Assert.Null(result.Exception);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RunTest_ScriptEndedTheTransaction_FailsWithTheMessageOfTheTest(bool inSetup)
	{
		// the data access fails the script that ended the transaction; its message speaks of a script, not of the test.
		MockSet mockSet = CreateMockSet();
		DbLiveTransactionEndedException scriptError = new("for example ...");
		if (inSetup)
		{
			mockSet.DbLiveDA.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
				.Returns(Task.FromException(scriptError));
		}
		else
		{
			mockSet.DbLiveDA.ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
				.Returns(Task.FromException<List<SqlResult>>(scriptError));
		}

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = GetFileData("/test/folder/setup.test.sql")
		});

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.ErrorMessage);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.Output);
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
		_ = mockSet.UnitTestResultChecker.DidNotReceive().ValidateTestResult(Arg.Any<List<SqlResult>>());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RunTest_ScriptLeftATransactionOpen_FailsWithTheMessageOfTheTest(bool inSetup)
	{
		// the data access fails the script that left a transaction open; its message speaks of a script, not of the test.
		MockSet mockSet = CreateMockSet();
		DbLiveTransactionLeftOpenException scriptError = new("it ran BEGIN without COMMIT", "DbLive rolled back ...");
		if (inSetup)
		{
			mockSet.DbLiveDA.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
				.Returns(Task.FromException(scriptError));
		}
		else
		{
			mockSet.DbLiveDA.ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
				.Returns(Task.FromException<List<SqlResult>>(scriptError));
		}

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = GetFileData("/test/folder/setup.test.sql")
		});

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionLeftOpenMessage, result.ErrorMessage);
		Assert.Equal(UnitTestItemRunner.TransactionLeftOpenMessage, result.Output);
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
		_ = mockSet.UnitTestResultChecker.DidNotReceive().ValidateTestResult(Arg.Any<List<SqlResult>>());
	}

	[Fact]
	public async Task RunTest_TransactionCheckThrows_Fails()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(Task.FromException<bool>(new InvalidOperationException("transaction is gone")));
		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		});

		Assert.False(result.IsSuccess);
		Assert.Equal("transaction is gone", result.ErrorMessage);
		_ = Assert.IsType<InvalidOperationException>(result.Exception);
	}

	[Fact]
	public async Task RunTest_TransactionTimedOut_FailsWithTheTimeoutOfTheTransaction()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			UnitTestItemTimeout = TimeSpan.FromMilliseconds(100)
		});
		mockSet.DbLiveDA.ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(async _ =>
			{
				await TransactionRunnerTests.WaitUntilAbortedAsync();
				return new List<SqlResult>();
			});
		// the check after the test opens a connection, which fails in an aborted transaction.
		TransactionException stepError = new("The operation is not valid for the state of the transaction.");
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(Task.FromException<bool>(stepError));

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		});

		// the transaction of the test is twice UnitTestItemTimeout: setup.test.sql and the test get UnitTestItemTimeout each.
		Assert.False(result.IsSuccess);
		Assert.Equal(
			"Execution Timeout Expired. The transaction of the test ran longer than its timeout of 00:00:00.2000000 (twice UnitTestItemTimeout).",
			result.ErrorMessage
		);
		ExecutionTimeoutException timeoutEx = Assert.IsType<ExecutionTimeoutException>(result.Exception);
		Assert.Equal(TimeSpan.FromMilliseconds(200), timeoutEx.Timeout);
		Assert.Same(stepError, timeoutEx.InnerException);
	}

	[Fact]
	public async Task RunTest_TimeoutTooLargeToDouble_TransactionGetsTheLargestTimeout()
	{
		MockSet mockSet = CreateMockSet();
		// settings.json takes any time span up to TimeSpan.MaxValue: "10675199.00:00:00", meant as no limit.
		TimeSpan timeout = TimeSpan.FromDays(10675199);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			UnitTestItemTimeout = timeout
		});
		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<List<SqlResult>>())
			.Returns(new ValidationResult { IsValid = true });

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		});

		// twice the timeout would overflow TimeSpan: the transaction gets TimeSpan.MaxValue, the test its own timeout.
		Assert.True(result.IsSuccess, result.ErrorMessage);
		_ = await mockSet.DbLiveDA.Received().ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), timeout);
	}

	[Fact]
	public async Task RunTest_TestScriptFails_TransactionNotChecked()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.DbLiveDA
			.When(fake => fake.ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>()))
			.Throw(new InvalidOperationException("test error"));

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			SetupFileData = null
		});

		// the error of the test is the result.
		_ = await mockSet.DbLiveDA.DidNotReceive().IsTransactionUsableAsync();
		Assert.False(result.IsSuccess);
		Assert.Equal("test error", result.ErrorMessage);
	}

	[Fact]
	public async Task RunTest_SetupFileFails_ErrorMessageNamesTheSetupFile()
	{
		MockSet mockSet = CreateMockSet();
		InvalidOperationException setupError = new("setup error");
		mockSet.DbLiveDA.ExecuteNonQueryAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException(setupError));

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "select-orders.test.sql",
			FileData = GetFileData("Tests/Orders/select-orders.test.sql"),
			SetupFileData = GetFileData("Tests/Orders/setup.test.sql")
		});

		// [SqlFact] fails with ErrorMessage and RunAllTestsAsync logs it: both say that the setup file failed, not the test.
		Assert.False(result.IsSuccess);
		Assert.Equal("Setup file error (Tests/Orders/setup.test.sql): setup error", result.ErrorMessage);
		Assert.StartsWith("ERROR: Setup file error (Tests/Orders/setup.test.sql): setup error", result.Output);
		Assert.Same(setupError, result.Exception);
		_ = await mockSet.DbLiveDA.DidNotReceive().ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());
	}

	[Fact]
	public async Task RunTest_TestFileFailsAfterSetup_ErrorMessageIsTheErrorOfTheTest()
	{
		MockSet mockSet = CreateMockSet();
		mockSet.DbLiveDA.ExecuteQueryMultipleAsync(Arg.Any<string>(), Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>())
			.Returns(Task.FromException<List<SqlResult>>(new InvalidOperationException("test error")));

		UnitTestItemRunner runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

		TestRunResult result = await runner.RunTestAsync(new TestItem
		{
			Name = "select-orders.test.sql",
			FileData = GetFileData("Tests/Orders/select-orders.test.sql"),
			SetupFileData = GetFileData("Tests/Orders/setup.test.sql")
		});

		Assert.False(result.IsSuccess);
		Assert.Equal("test error", result.ErrorMessage);
		Assert.StartsWith("ERROR: test error", result.Output);
	}

	/// <summary>
	/// The transaction of the test is still open after the test unless a test sets otherwise.
	/// </summary>
	private static MockSet CreateMockSet()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.IsTransactionUsableAsync().Returns(true);
		return mockSet;
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