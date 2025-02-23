using EasyFlow.Adapter;
using EasyFlow.Deployers.Testing;
namespace EasyFlow.Tests.Testing;

public class UnitTestResultCheckerTests
{
	[Fact]
	public void RunTest()
	{
		// Arrange
		//MockSet mockSet = new();

		//mockSet.EasyFlowProject.GetTests().Returns([
		//	new TestItem { Name = "first", FileData = GetFileData("/test/first.sql") },
		//	new TestItem { Name = "second", FileData = GetFileData("/test/second.sql") },
		//	new TestItem { Name = "third", FileData = GetFileData("/test/third.sql") }
		//]);

		//mockSet.UnitTestItemRunner.RunTest(Arg.Any<TestItem>())
		//	.Returns(new TestRunResult { IsSuccess = true });

		//Action<string> writeLine = Console.WriteLine;

		////var tester = mockSet.CreateUsingMocks<EasyFlowTester>();
		//MultipleResults results = new();
		//results.Add(new SqlResult());

		//var testResultChecker = new UnitTestResultChecker();

		//// Act
		//testResultChecker.

		//// Assert
		//mockSet.EasyFlowProject.Received().GetTests();
	}

}