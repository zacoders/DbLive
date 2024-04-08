using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public class UnitTestsRunner(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		IUnitTestItemRunner _unitTestItemRunner
	) : IUnitTestsRunner
{
	private readonly ILogger Logger = _logger.ForContext(typeof(UnitTestItemRunner));

	public void RunAllTests(DeployParameters parameters)
	{
		if (!parameters.RunTests)
		{
			return;
		}

		Logger.Information("Running Tests.");

		var tests = _project.GetTests();

		TestsRunResults runResults = new();

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForTestsRun };

		Parallel.ForEach(tests, parallelOptions, test =>
		{
			var testResult = _unitTestItemRunner.RunTest(test);

			if (testResult.IsSuccess)
			{
				runResults.IncremenPassed();
				Logger.Information("PASSED Test: {testName}", test.Name);
			}
			else
			{
				runResults.IncremenFailed();
				Logger.Error(testResult.Exception, "FAILED Test: {filePath}. Error Message: {errorMessage}", test.Name, testResult.ErrorMessage);
			}

			_da.SaveUnitTestResult(
				new UnitTestItemDto
				{
					RelativePath = test.FileData.RelativePath,
					Crc32Hash = test.FileData.Crc32Hash,
					StartedUtc = testResult.StartedUtc,
					ExecutionTimeMs = testResult.ExecutionTimeMs,
					IsSuccess = testResult.IsSuccess,
					ErrorMessage = testResult.ErrorMessage
				}
			);
		});

		Logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new EasyFlowSqlException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}
}
