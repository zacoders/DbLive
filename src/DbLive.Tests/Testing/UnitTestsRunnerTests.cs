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
				InitFileData = GetFileData("/test/init.sql")
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

	private static FileData GetFileData(string relativePath)
	{
		return new FileData
		{
			Content = $"-- unit test mock content {Guid.NewGuid()}",
			RelativePath = relativePath,
			FilePath = "c:/somepath" + relativePath
		};
	}
}