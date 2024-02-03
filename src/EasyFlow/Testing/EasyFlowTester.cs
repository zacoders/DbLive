using EasyFlow.Deployers.Tests;

namespace EasyFlow.Testing;

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
		var testItem = TestsList[relativePath];

		if (testItem.InitFileData is not null)
		{
			string initFileTitle = $"== Init file: {testItem.InitFileData.RelativePath} ==";
			writeLine(new string('-', initFileTitle.Length));
			writeLine(initFileTitle);
			writeLine(new string('-', initFileTitle.Length));
			writeLine($"{testItem.InitFileData?.Content}");
			writeLine("");
		}

		string testFileTitle = $"== Test file: {relativePath} ==";
		writeLine(new string('-', testFileTitle.Length));
		writeLine(testFileTitle);
		writeLine(new string('-', testFileTitle.Length));

		writeLine($"{testItem.FileData.Content}");
		writeLine("");

		var testRunResult = _unitTestsRunner.RunTest(testItem, new EasyFlowSettings());

		writeLine("------------------");
		writeLine("== Test output: ==");
		writeLine("------------------");
		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
