using DbLive.Deployers;
using DbLive.SelfDeployer;

namespace DbLive.Testing;

/// <summary>
/// Runs the SQL tests of the project against a deployed database: all of them (a CI step after the deployment),
/// or one by one from xUnit (<c>[SqlFact]</c>).
/// </summary>
internal class DbLiveTester(
		IDbLiveProject _project,
		IUnitTestItemRunner _unitTestItemRunner,
		IUnitTestsRunner _unitTestsRunner,
		IDbLiveInternalDA _da,
		ISettingsAccessor _projectSettings,
		IProjectIdValidator _projectIdValidator,
		ITransactionSettingsValidator _transactionSettingsValidator
	) : IDbLiveTester
{
	private ReadOnlyDictionary<string, TestItem>? _testsList = null;

	public async Task RunAllTestsAsync()
	{
		await _da.CheckDatabaseExistsAsync().ConfigureAwait(false);

		// otherwise the first test fails on saving its result to dblive.unit_test_run.
		if (!await _da.DbLiveInstalledAsync().ConfigureAwait(false))
		{
			throw new DatabaseNotDeployedException();
		}

		// the tests write dblive.unit_test_run, which a newer DbLive may have changed. Only checked: a deployment writes the requirement.
		DbLiveCompatibility.Check(await _da.GetMinDbLiveLibVersionAsync().ConfigureAwait(false));

		await _projectIdValidator.ValidateBeforeTestsAsync().ConfigureAwait(false);

		DbLiveSettings projectSettings = await _projectSettings.GetProjectSettingsAsync().ConfigureAwait(false);

		await _transactionSettingsValidator.ValidateAsync(projectSettings).ConfigureAwait(false);

		await _unitTestsRunner.RunAllTestsAsync().ConfigureAwait(false);
	}

	private async Task<ReadOnlyDictionary<string, TestItem>> LoadTestsAsync()
	{
		IReadOnlyCollection<TestItem> tests =
			await _project.GetTestsAsync().ConfigureAwait(false);

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

		if (testItem.SetupFileData is not null)
		{
			string setupFileTitle = $"Setup file: {testItem.SetupFileData.RelativePath}";
			writeLine(setupFileTitle);
		}

		TestRunResult testRunResult = await _unitTestItemRunner.RunTestAsync(testItem).ConfigureAwait(false);

		writeLine(testRunResult.Output);

		return testRunResult;
	}
}
