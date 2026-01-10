
namespace DbLive.Testing;

/// <summary>
/// This class used to run unit tests in Visual Studio. Designed for xunit.
/// </summary>
public class DbLiveTester : IDbLiveTester
{
	private readonly IDbLiveProject _project;
	private readonly IUnitTestItemRunner _unitTestItemRunner;

	private ReadOnlyDictionary<string, TestItem>? _testsList = null;

	public DbLiveTester(
		IDbLiveProject project,
		IUnitTestItemRunner unitTestItemRunner)
	{
		_project = project;
		_unitTestItemRunner = unitTestItemRunner;
	}

	private async Task<ReadOnlyDictionary<string, TestItem>> LoadTestsAsync()
	{
		IReadOnlyCollection<TestItem> tests = await _project.GetTestsAsync().ConfigureAwait(false);

		return new ReadOnlyDictionary<string, TestItem>(
			tests.ToDictionary(
				test => test.FileData.RelativePath,
				test => test
			)
		);
	}

	private async Task<ReadOnlyDictionary<string, TestItem>> GetTestsAsync()
	{
		if (_testsList is null)
		{
			_testsList = await LoadTestsAsync().ConfigureAwait(false);
		}

		return _testsList;
	}

	public async Task<TestRunResult> RunTestAsync(Action<string> writeLine, string testFileRelativePath)
	{
		ReadOnlyDictionary<string, TestItem> testsList = await GetTestsAsync().ConfigureAwait(false);

		TestItem testItem = testsList[testFileRelativePath];

		if (testItem.InitFileData is not null)
		{
			string initFileTitle = $"Init file: {testItem.InitFileData.RelativePath}";
			writeLine(initFileTitle);
		}

		TestRunResult testRunResult = await _unitTestItemRunner.RunTestAsync(testItem).ConfigureAwait(false);

		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
