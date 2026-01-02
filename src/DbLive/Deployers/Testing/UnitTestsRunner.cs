
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
	private readonly DbLiveSettings _projectSettings = settingsAccessor.ProjectSettings;

	public void RunAllTests(DeployParameters parameters)
	{
		if (!parameters.RunTests)
		{
			return;
		}

		_logger.Information("Running Tests.");

		var tests = _project.GetTests();

		TestsRunResults runResults = new();

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _projectSettings.NumberOfThreadsForTestsRun };

		Parallel.ForEach(tests, parallelOptions, test =>
		{
			var testResult = _unitTestItemRunner.RunTest(test);

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

			_da.SaveUnitTestResult(
				new UnitTestItemDto
				{
					RelativePath = test.FileData.RelativePath,
					Crc32Hash = test.FileData.ContentHash,
					StartedUtc = testResult.StartedUtc,
					ExecutionTimeMs = testResult.ExecutionTimeMs,
					IsSuccess = testResult.IsSuccess,
					ErrorMessage = testResult.ErrorMessage
				}
			);
		});

		_logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new DbLiveSqlException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}
}
