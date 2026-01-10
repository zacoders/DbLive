
namespace DbLive.Testing;

/// <summary>
/// This class used to run unit tests in Visual Studio. Designed for xunit.
/// </summary>
public class DbLiveTester : IDbLiveTester
{
	private readonly IDbLiveProject _project;
	private readonly IUnitTestItemRunner _unitTestItemRunner;

	private readonly Lazy<Task<ReadOnlyDictionary<string, TestItem>>> _testsListLazy;

	public DbLiveTester(
		IDbLiveProject project,
		IUnitTestItemRunner unitTestItemRunner)
	{
		_project = project;
		_unitTestItemRunner = unitTestItemRunner;

		_testsListLazy = new Lazy<Task<ReadOnlyDictionary<string, TestItem>>>(LoadTestsAsync);
	}

	private async Task<ReadOnlyDictionary<string, TestItem>> LoadTestsAsync()
	{
		IReadOnlyCollection<TestItem> tests = await _project.GetTestsAsync();

		return new ReadOnlyDictionary<string, TestItem>(
			tests.ToDictionary(
				test => test.FileData.RelativePath,
				test => test
			)
		);
	}

	private Task<ReadOnlyDictionary<string, TestItem>> GetTestsAsync()
	{
		return _testsListLazy.Value;
	}

	public async Task<TestRunResult> RunTestAsync(Action<string> writeLine, string testFileRelativePath)
	{
		ReadOnlyDictionary<string, TestItem> testsList = await GetTestsAsync();

		TestItem testItem = testsList[testFileRelativePath];

		if (testItem.InitFileData is not null)
		{
			string initFileTitle = $"Init file: {testItem.InitFileData.RelativePath}";
			writeLine(initFileTitle);
		}

		TestRunResult testRunResult = await _unitTestItemRunner.RunTestAsync(testItem);

		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
