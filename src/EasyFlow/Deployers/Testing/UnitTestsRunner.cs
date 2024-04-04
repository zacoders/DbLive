using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public class UnitTestsRunner(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider
	) : IUnitTestsRunner
{
	private readonly ILogger Logger = _logger.ForContext(typeof(UnitTestsRunner));

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30); // test should be fast by default 

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
			var testResult = RunTest(test);

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
				test.FileData.RelativePath,
				test.FileData.Crc32Hash,
				testResult.StartedUtc,
				testResult.ExecutionTimeMs,
				testResult.IsSuccess,
				testResult.ErrorMessage
			);
		});

		Logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new EasyFlowSqlException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}

	public TestRunResult RunTest(TestItem test)
	{
		TestRunResult result = new()
		{
			IsSuccess = false,
			ErrorMessage = null,
			StartedUtc = _timeProvider.UtcNow()
		};

		var stopWatch = _timeProvider.StartNewStopwatch();
		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(TranIsolationLevel.Serializable, _defaultTimeout);

			if (test.InitFileData is not null)
			{
				_da.ExecuteNonQuery(test.InitFileData.Content);
			}

			_da.ExecuteNonQuery(test.FileData.Content);

			result.Output = "TODO: Populate it using output from sql execution.";

			result.IsSuccess = true;

			_transactionScope.Dispose(); //canceling transaction
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
			result.Output = "ERROR:" + ex.Message + Environment.NewLine + result.Output;
			result.Exception = ex;
		}
		finally
		{
			stopWatch.Stop();
			result.ExecutionTimeMs = stopWatch.ElapsedMilliseconds;
			result.CompletedUtc = _timeProvider.UtcNow();
		}

		return result;
	}
}
