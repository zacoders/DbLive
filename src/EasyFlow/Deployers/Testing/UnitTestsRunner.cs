using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public class UnitTestsRunner(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		IUnitTestItemRunner _unitTestItemRunner
	) : IUnitTestsRunner
{
	private readonly ILogger _logger = _logger.ForContext(typeof(UnitTestItemRunner));

	public void RunAllTests(DeployParameters parameters)
	{
		if (!parameters.RunTests)
		{
			return;
		}

		_logger.Information("Running Tests.");

		var tests = _project.GetTests();

		TestsRunResults runResults = new();

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForTestsRun };

		Parallel.ForEach(tests, parallelOptions, test =>
		{
			var testResult = _unitTestItemRunner.RunTest(test);

			if (testResult.IsSuccess)
			{
				runResults.IncremenPassed();
				_logger.Information("PASSED Test: {testName}", test.Name);
			}
			else
			{
				runResults.IncremenFailed();
				_logger.Error(testResult.Exception, "FAILED Test: {filePath}. Error Message: {errorMessage}", test.Name, testResult.ErrorMessage);
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

		_logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new EasyFlowSqlException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}
}
