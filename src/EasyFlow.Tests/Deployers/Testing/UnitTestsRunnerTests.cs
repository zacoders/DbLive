using EasyFlow.Adapter;
using EasyFlow.Deployers.Testing;

namespace EasyFlow.Tests.Deployers.Migrations;

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
		mockSet.EasyFlowProject.DidNotReceive().GetTests();
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

		mockSet.EasyFlowProject.GetTests().Returns([
			testItem1,
			testItem2
		]);


		mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
			.Returns(new TestRunResult() { IsSuccess = true});

		DeployParameters parameters = new() { RunTests = true };

		// Act
		runner.RunAllTests(parameters);

		// Assert
		mockSet.EasyFlowProject.Received().GetTests();
		mockSet.EasyFlowDA.Received(2).SaveUnitTestResult(Arg.Any<UnitTestItemDto>());
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