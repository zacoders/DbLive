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
		IDbLiveDA _da,
		ISettingsAccessor _projectSettings,
		IProjectIdValidator _projectIdValidator,
		ITransactionSettingsValidator _transactionSettingsValidator
	) : IDbLiveTester
{
	private ReadOnlyDictionary<string, TestItem>? _testsList = null;

	public async Task RunAllTestsAsync()
	{
		// Pooling=False (SQL Server) or Enlist=False: every test would fail, under an error of the provider that does not name it.
		_da.CheckConnectionString();

		await _da.CheckDatabaseExistsAsync().ConfigureAwait(false);

		// a database without the DbLive tables was never deployed, and the check of the ProjectId reads dblive.version.
		if (!await _da.DbLiveInstalledAsync().ConfigureAwait(false))
		{
			throw new DatabaseNotDeployedException();
		}

		// the check of the ProjectId reads dblive.version, which a newer DbLive may have changed. Only checked: a deployment writes the requirement.
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

		// a test is identified by its relative path ignoring case, as DbLive finds files in the project; the project load
		// rejects two paths that differ only by case (ScriptPathsDifferOnlyByCaseException).
		return new ReadOnlyDictionary<string, TestItem>(
			tests.ToDictionary(
				test => test.FileData.RelativePath,
				test => test,
				StringComparer.OrdinalIgnoreCase
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

		// the project has relative paths with '/' on every OS; a caller on Windows may pass '\'.
		if (!testsList.TryGetValue(testFileRelativePath.Replace('\\', '/'), out TestItem? testItem))
		{
			throw new KeyNotFoundException(
				$"Test '{testFileRelativePath}' not found in the project. Pass the path of the test file relative to the scripts folder, for example 'Tests/Orders/select-order.test.sql'."
			);
		}

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
