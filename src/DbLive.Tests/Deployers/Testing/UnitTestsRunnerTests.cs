using Microsoft.IdentityModel.Tokens;
using Xunit.Sdk;

namespace DbLive.Tests.Deployers.Testing;

public class UnitTestsRunnerTests
{
	[Fact]
	public async Task RunTest_Tests_Disabled()
	{
		// Arrange
		MockSet mockSet = new();

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		DeployParameters parameters = new() { RunTests = false };

		// Act
		await runner.RunAllTestsAsync(parameters);

		// Assert
		await mockSet.DbLiveProject.DidNotReceive().GetTestsAsync();
	}


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
			InitFileData = GetFileData("/tests/init.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		DeployParameters parameters = new() { RunTests = true };

		// Act
		await runner.RunAllTestsAsync(parameters);

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();
		await mockSet.DbLiveDA.Received(2).SaveUnitTestResultAsync(Arg.Any<UnitTestItemDto>());
	}


	[Fact]
	public async Task RunTest()
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
			InitFileData = GetFileData("/tests/init.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		DeployParameters parameters = new() { RunTests = true };

		// Act
		await runner.RunAllTestsAsync(parameters);

		// Assert
		await mockSet.DbLiveProject.Received().GetTestsAsync();

		await mockSet.DbLiveDA.Received(2).SaveUnitTestResultAsync(Arg.Any<UnitTestItemDto>());

		await mockSet.DbLiveDA.Received()
			.SaveUnitTestResultAsync(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem1.FileData.RelativePath &&
				dto.ContentHash == testItem1.FileData.ContentHash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		await mockSet.DbLiveDA.Received()
			.SaveUnitTestResultAsync(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem2.FileData.RelativePath &&
				dto.ContentHash == testItem2.FileData.ContentHash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		mockSet.Logger.Received()
			.Information(
				Arg.Is("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}."),
				Arg.Is(2), // passed
				Arg.Is(0)  // failed
			);
	}


	[Fact]
	public async Task RunTest_OneTestFailed_RunsAllTests_AndSavesAllResults()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			NumberOfThreadsForTestsRun = 1
		});

		UnitTestsRunner runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		TestItem testItem1 = new()
		{
			Name = "test1",
			FileData = GetFileData("/tests/test1.sql")
		};

		TestItem testItem2 = new()
		{
			Name = "test2",
			FileData = GetFileData("/tests/test2.sql"),
			InitFileData = GetFileData("/tests/init.sql")
		};

		TestItem testItem3 = new()
		{
			Name = "test3",
			FileData = GetFileData("/tests/test3.sql")
		};

		mockSet.DbLiveProject.GetTestsAsync().Returns([testItem1, testItem2, testItem3]);

		mockSet.UnitTestItemRunner.RunTestAsync(Arg.Any<TestItem>())
			.Returns(call =>
			{
				TestItem item = call.Arg<TestItem>();
				return Task.FromResult(new TestRunResult
				{
					IsSuccess = item.Name != "test2",
					ErrorMessage = item.Name == "test2" ? "failed" : null
				});
			});

		await Assert.ThrowsAsync<UnitTestsFailedException>(() => runner.RunAllTestsAsync(new DeployParameters { RunTests = true }));

		await mockSet.UnitTestItemRunner.Received(3).RunTestAsync(Arg.Any<TestItem>());
		await mockSet.DbLiveDA.Received(3).SaveUnitTestResultAsync(Arg.Any<UnitTestItemDto>());

		await mockSet.DbLiveDA.Received()
			.SaveUnitTestResultAsync(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem1.FileData.RelativePath &&
				dto.ContentHash == testItem1.FileData.ContentHash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		await mockSet.DbLiveDA.Received()
			.SaveUnitTestResultAsync(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem2.FileData.RelativePath &&
				dto.ContentHash == testItem2.FileData.ContentHash &&
				dto.IsSuccess == false &&
				dto.ErrorMessage == "failed"
			));

		await mockSet.DbLiveDA.Received()
			.SaveUnitTestResultAsync(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem3.FileData.RelativePath &&
				dto.ContentHash == testItem3.FileData.ContentHash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		mockSet.Logger.Received()
			.Information(
				Arg.Is("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}."),
				Arg.Is(2), // passed
				Arg.Is(1)  // failed
			);
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