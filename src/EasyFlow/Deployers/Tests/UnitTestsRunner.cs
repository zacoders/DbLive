using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Tests;

public class UnitTestsRunner(
		ILogger _logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider
	) : IUnitTestsRunner
{
	private readonly ILogger Logger = _logger.ForContext(typeof(UnitTestsRunner));

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30); // test should be fast by default 

	public void RunAllTests(DeployParameters parameters, EasyFlowSettings settings)
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
			var testResult = RunTest(test, settings);

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

			SaveTestResult(test, testResult);
		});

		Logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			runResults.PassedCount, runResults.FailedCount);

		if (runResults.FailedCount > 0)
		{
			throw new EasyFlowSqlException($"Unit test run failed. Failed test count {runResults.FailedCount} of {runResults.Total}.");
		}
	}

	private void SaveTestResult(TestItem test, TestRunResult result)
	{
		_da.SaveUnitTestResult(
			test.FileData.RelativePath,
			test.FileData.Crc32Hash,
			result.StartedUtc,
			result.ExecutionTimeMs,
			result.IsSuccess,
			result.ErrorMessage
		);
	}

	public TestRunResult RunTest(TestItem test, EasyFlowSettings settings)
	{
		TestRunResult result = new()
		{
			IsSuccess = false,
			ErrorMessage = null,
			StartedUtc = _timeProvider.UtcNow()
		};

		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(settings.TestsTransactionIsolationLevel, _defaultTimeout);

			if (test.InitFileData is not null)
			{
				_da.ExecuteNonQuery(test.InitFileData.Content);
			}
			_da.ExecuteNonQuery("select 1");
			_da.ExecuteNonQuery(test.FileData.Content);

			_transactionScope.Dispose(); //canceling transaction

			result.Output = "TODO: Populate it using output from sql execution.";

			result.IsSuccess = true;
		}
		catch (Exception ex)
		{
			result.ErrorMessage = ex.Message;
			result.Output = "ERROR:" + ex.Message + Environment.NewLine + result.Output;
			result.Exception = ex;
		}

		result.CompletedUtc = _timeProvider.UtcNow();

		return result;
	}
}
