

namespace DbLive.Deployers.Testing;

public class UnitTestsRunner(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		IUnitTestItemRunner _unitTestItemRunner,
		ISettingsAccessor settingsAccessor
	) : IUnitTestsRunner
{
	private readonly ILogger _logger = _logger.ForContext(typeof(UnitTestsRunner));

	public async Task RunAllTestsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		_logger.Information("Running Tests.");

		IReadOnlyCollection<TestItem> tests = await _project.GetTestsAsync().ConfigureAwait(false);

		TestsRunResults runResults = new();
		ConcurrentBag<string> failedTests = [];

		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = Math.Max(1, projectSettings.NumberOfThreadsForTestsRun)
		};

		await Parallel.ForEachAsync(tests, parallelOptions, async (test, ct) =>
		{
			ct.ThrowIfCancellationRequested();

			TestRunResult testResult = await _unitTestItemRunner.RunTestAsync(test).ConfigureAwait(false);

			if (testResult.IsSuccess)
			{
				runResults.IncremenPassed();
				_logger.Information("PASSED: {testName}", test.Name);
			}
			else
			{
				runResults.IncremenFailed();
				failedTests.Add(test.FileData.RelativePath);
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
			throw new UnitTestsFailedException(
				runResults.FailedCount,
				runResults.Total,
				[.. failedTests.Order(StringComparer.Ordinal)]
			);
		}
	}
}
