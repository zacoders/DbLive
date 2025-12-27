using Microsoft.IdentityModel.Tokens;
using Xunit.Sdk;

namespace DbLive.Tests.Deployers.Testing;

public class UnitTestsRunnerTests
{
	[Fact]
	public void RunTest_Tests_Disabled()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

		DeployParameters parameters = new() { RunTests = false };

		// Act
		runner.RunAllTests(parameters);

		// Assert
		mockSet.DbLiveProject.DidNotReceive().GetTests();
	}


	[Fact]
	public void RunTest_Simple_Success()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

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

		mockSet.DbLiveProject.GetTests().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		DeployParameters parameters = new() { RunTests = true };

		// Act
		runner.RunAllTests(parameters);

		// Assert
		mockSet.DbLiveProject.Received().GetTests();
		mockSet.DbLiveDA.Received(2).SaveUnitTestResult(Arg.Any<UnitTestItemDto>());
	}


	[Fact]
	public void RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

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

		mockSet.DbLiveProject.GetTests().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true });

		DeployParameters parameters = new() { RunTests = true };

		// Act
		runner.RunAllTests(parameters);

		// Assert
		mockSet.DbLiveProject.Received().GetTests();

		mockSet.DbLiveDA.Received(2).SaveUnitTestResult(Arg.Any<UnitTestItemDto>());

		mockSet.DbLiveDA.Received()
			.SaveUnitTestResult(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem1.FileData.RelativePath &&
				dto.Crc32Hash == testItem1.FileData.Crc32Hash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		mockSet.DbLiveDA.Received()
			.SaveUnitTestResult(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem2.FileData.RelativePath &&
				dto.Crc32Hash == testItem2.FileData.Crc32Hash &&
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
	public void RunTest_OneTestFailed()
	{
		// Arrange
		MockSet mockSet = new();

		var runner = mockSet.CreateUsingMocks<UnitTestsRunner>();

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

		mockSet.DbLiveProject.GetTests().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTest(Arg.Is(testItem1))
			.Returns(new TestRunResult() { IsSuccess = true });

		mockSet.UnitTestItemRunner.RunTest(Arg.Is(testItem2))
			.Returns(new TestRunResult() { IsSuccess = false });

		DeployParameters parameters = new() { RunTests = true };

		// Act
		Assert.Throws<DbLiveSqlException>(() => runner.RunAllTests(parameters));

		// Assert

		mockSet.DbLiveProject.Received().GetTests();

		mockSet.DbLiveDA.Received(2).SaveUnitTestResult(Arg.Any<UnitTestItemDto>());

		mockSet.DbLiveDA.Received()
			.SaveUnitTestResult(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem1.FileData.RelativePath &&
				dto.Crc32Hash == testItem1.FileData.Crc32Hash &&
				dto.IsSuccess == true &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		mockSet.DbLiveDA.Received()
			.SaveUnitTestResult(Arg.Is<UnitTestItemDto>(dto =>
				dto.RelativePath == testItem2.FileData.RelativePath &&
				dto.Crc32Hash == testItem2.FileData.Crc32Hash &&
				dto.IsSuccess == false &&
				dto.ErrorMessage.IsNullOrEmpty()
			));

		mockSet.Logger.Received()
			.Information(
				Arg.Is("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}."),
				Arg.Is(1), // passed
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