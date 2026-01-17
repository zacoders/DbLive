

namespace DbLive.Deployers.Testing;

public class UnitTestsRunner(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IUnitTestItemRunner _unitTestItemRunner,
		ISettingsAccessor settingsAccessor
	) : IUnitTestsRunner
{
	private readonly ILogger _logger = _logger.ForContext(typeof(UnitTestItemRunner));

	public async Task RunAllTestsAsync(DeployParameters parameters)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		if (!parameters.RunTests)
		{
			return;
		}

		_logger.Information("Running Tests.");

		IReadOnlyCollection<TestItem> tests = await _project.GetTestsAsync().ConfigureAwait(false);

		TestsRunResults runResults = new();

		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = projectSettings.NumberOfThreadsForTestsRun
		};

		await Parallel.ForEachAsync(tests, parallelOptions, async (test, ct) =>
		{
			TestRunResult testResult = await _unitTestItemRunner.RunTestAsync(test).ConfigureAwait(false);

			if (testResult.IsSuccess)
			{
				runResults.IncremenPassed();
				_logger.Information("PASSED: {testName}", test.Name);
			}
			else
			{
				runResults.IncremenFailed();
				_logger.Error(testResult.Exception, "FAILED: {filePath}. Error Message: {errorMessage}", test.Name, testResult.ErrorMessage);
			}

			await _da.SaveUnitTestResultAsync(
				new UnitTestItemDto
				{
					RelativePath = test.FileData.RelativePath,
					ContentHash = test.FileData.ContentHash,
					StartedUtc = testResult.StartedUtc,
					ExecutionTimeMs = testResult.ExecutionTimeMs,
					IsSuccess = testResult.IsSuccess,
					ErrorMessage = testResult.ErrorMessage
				}
			).ConfigureAwait(false);
		}).ConfigureAwait(false);

		_logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new UnitTestsFailedException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}
}
