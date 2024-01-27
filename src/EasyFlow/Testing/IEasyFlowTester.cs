using EasyFlow.Deployers.Tests;
using System.Collections.ObjectModel;

namespace EasyFlow.Testing;

public interface IEasyFlowTester
{
	TestRunResult RunTest(Action<string> writeLine, string relativePath);
}

public class EasyFlowTester(IEasyFlowProject _project, IUnitTestsRunner _unitTestsRunner)
	: IEasyFlowTester
{
	private readonly ReadOnlyDictionary<string, TestItem> TestsList = new(
		_project.GetTests().ToDictionary(i => i.FileData.RelativePath, i => i)
	);

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="output"><see cref="ITestOutput"/></param>
	/// <param name="relativePath">Relative path to the sql test.</param>
	public TestRunResult RunTest(Action<string> writeLine, string relativePath)
	{
		writeLine($"Running unit test {relativePath}");
		writeLine("");

		var testItem = TestsList[relativePath];

		writeLine($"{testItem.FileData.Content}");

		var testRunResult = _unitTestsRunner.RunTest(testItem, new EasyFlowSettings());

		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
