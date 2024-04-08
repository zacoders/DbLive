using EasyFlow.Deployers.Testing;
using EasyFlow.Project;
using EasyFlow.Testing;
namespace EasyFlow.Tests.Deployers.Migrations;

public class EasyFlowTesterTests
{
	[Fact]
	public void RunTest()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowProject.GetTests().Returns([
			new TestItem { Name = "first", FileData = GetFileData("/test/first.sql") },
			new TestItem { Name = "second", FileData = GetFileData("/test/second.sql") },
			new TestItem { Name = "third", FileData = GetFileData("/test/third.sql") }
		]);

		mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = true });

		Action<string> writeLine = Console.WriteLine;

		var tester = mockSet.CreateUsingMocks<EasyFlowTester>();

		// Act
		tester.RunTest(writeLine, "/test/first.sql");

		// Assert
		mockSet.EasyFlowProject.Received().GetTests();
	}


	[Fact]
	public void RunTest_WithInit()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowProject.GetTests().Returns([
			new TestItem { 
				Name = "first", 
				FileData = GetFileData("/test/first.sql"),
				InitFileData = GetFileData("/test/init.sql")
			},
			new TestItem { Name = "second", FileData = GetFileData("/test/second.sql") },
			new TestItem { Name = "third", FileData = GetFileData("/test/third.sql") }
		]);

		mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
			.Returns(new TestRunResult { IsSuccess = true });

		Action<string> writeLine = Console.WriteLine;

		var tester = mockSet.CreateUsingMocks<EasyFlowTester>();

		// Act
		tester.RunTest(writeLine, "/test/first.sql");

		// Assert
		mockSet.EasyFlowProject.Received().GetTests();
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