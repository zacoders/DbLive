using EasyFlow.Adapter;
using EasyFlow.Deployers.Testing;

namespace EasyFlow.Tests.Deployers.Testing;

public class UnitTestItemRunnerTests
{
	[Fact]
	public void RunTest()
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

		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<MultipleResults>())
			.Returns(new ValidationResult { CompareResult = CompareResult.Match });

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			InitFileData = null
		};

		// Act
		TestRunResult result = runner.RunTest(testItem);


		// Assert
		mockSet.EasyFlowDA.Received(1);
		mockSet.EasyFlowDA.Received().ExecuteQueryMultiple(Arg.Is(testItem.FileData.Content));
		mockSet.UnitTestResultChecker.Received().ValidateTestResult(Arg.Any<MultipleResults>());

		Assert.True(result.IsSuccess);
		Assert.Equal(startUtc, result.StartedUtc);
		Assert.Equal(endUtc, result.CompletedUtc);
		Assert.Empty(result.ErrorMessage);
		Assert.Equal(999, result.ExecutionTimeMs);
	}


	[Fact]
	public void RunTest_With_InitFileData()
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

		mockSet.UnitTestResultChecker.ValidateTestResult(Arg.Any<MultipleResults>())
			.Returns(new ValidationResult { CompareResult = CompareResult.Match });

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			InitFileData = GetFileData("/test/folder/init.sql"),
		};

		// Act
		TestRunResult result = runner.RunTest(testItem);


		// Assert
		mockSet.EasyFlowDA.Received(2);

		Received.InOrder(() =>
		{
			mockSet.EasyFlowDA.Received().ExecuteNonQuery(Arg.Is(testItem.InitFileData.Content));
			mockSet.EasyFlowDA.Received().ExecuteQueryMultiple(Arg.Is(testItem.FileData.Content));
		});

		Assert.True(result.IsSuccess);
		Assert.Equal(startUtc, result.StartedUtc);
		Assert.Equal(endUtc, result.CompletedUtc);
		Assert.Empty(result.ErrorMessage);
		Assert.Equal(999, result.ExecutionTimeMs);
	}

	[Fact]
	public void RunTest_TestFailed_Exception()
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

		mockSet.EasyFlowDA
			.When(fake => fake.ExecuteQueryMultiple(Arg.Any<string>()))
			.Throw<Exception>();

		TestItem testItem = new()
		{
			Name = "test1",
			FileData = GetFileData("/test/folder/test1.sql"),
			InitFileData = null
		};

		// Act
		TestRunResult result = runner.RunTest(testItem);


		// Assert
		mockSet.EasyFlowDA.Received(1);
		mockSet.EasyFlowDA.Received().ExecuteQueryMultiple(Arg.Is(testItem.FileData.Content));

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