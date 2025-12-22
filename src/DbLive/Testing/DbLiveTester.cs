using DbLive.Deployers.Testing;

namespace DbLive.Testing;

/// <summary>
/// This class used to run unit tests in Visual Studio. Designed for xunit.
/// </summary>
/// <param name="_project"></param>
/// <param name="_unitTestItemRunner"></param>
public class DbLiveTester(
		IDbLiveProject _project,
		IUnitTestItemRunner _unitTestItemRunner
	) : IDbLiveTester
{

	private readonly ReadOnlyDictionary<string, TestItem> TestsList = new(
		_project.GetTests().ToDictionary(test => test.FileData.RelativePath, i => i)
	);

	/// <summary>
	/// Runs Sql test.
	/// </summary>
	/// <param name="output"><see cref="ITestOutput"/></param>
	/// <param name="testFileRelativePath">Relative path to the sql test.</param>
	public TestRunResult RunTest(Action<string> writeLine, string testFileRelativePath)
	{
		var testItem = TestsList[testFileRelativePath];

		if (testItem.InitFileData is not null)
		{
			string initFileTitle = $"Init file: {testItem.InitFileData.RelativePath}";
			//writeLine(new string('-', initFileTitle.Length));
			writeLine(initFileTitle);
			//writeLine(new string('-', initFileTitle.Length));
			//writeLine(testItem.InitFileData.Content);
			//writeLine("");
		}

		//string testFileTitle = $"== Test file: {testFileRelativePath} ==";
		//writeLine(new string('-', testFileTitle.Length));
		//writeLine(testFileTitle);
		//writeLine(new string('-', testFileTitle.Length));

		//writeLine(testItem.FileData.Content);
		//writeLine("");

		var testRunResult = _unitTestItemRunner.RunTest(testItem);

		//writeLine("------------------");
		//writeLine("== Test output: ==");
		//writeLine("------------------");
		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
