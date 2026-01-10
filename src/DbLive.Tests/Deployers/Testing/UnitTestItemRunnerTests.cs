namespace DbLive.Tests.Deployers.Testing;

public class UnitTestItemRunnerTests
{
	[Fact]
	public async Task RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

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
			InitFileData = null
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
	public async Task RunTest_With_InitFileData()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

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
			InitFileData = GetFileData("/test/folder/init.sql"),
		};

		// Act
		TestRunResult result = await runner.RunTestAsync(testItem);


		// Assert
		mockSet.DbLiveDA.Received(2);

		Received.InOrder(() =>
		{
			mockSet.DbLiveDA.Received().ExecuteNonQueryAsync(
				Arg.Is(testItem.InitFileData.Content),
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
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestItemRunner>();

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
			InitFileData = null
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

	private static FileData GetFileData(string relativePath)
	{
		return new FileData
		{
			Content = $"-- unit test mock content {Guid.NewGuid()}",
			RelativePath = relativePath,
			FilePath = "c:/data" + relativePath
		};
	}
}