namespace EasyFlow.Deployers;

public class UnitTestsRunner
{
	private static readonly ILogger Logger = Log.ForContext(typeof(UnitTestsRunner));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowProject _project;
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30); // test should be fast by default 

	public UnitTestsRunner(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
	}

	public void RunTests(string sqlConnectionString, DeployParameters parameters, EasyFlowSettings settings)
	{
		if (!parameters.RunTests)
		{
			return;
		}

		Logger.Information("Running Tests.");

		var tests = _project.GetTests();

		TestRunResult result = new();

		var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = parameters.NumberOfThreadsForTestsRun };

		Parallel.ForEach(tests, parallelOptions, test =>
		{
			bool isSuccess = RunTest(test, sqlConnectionString, settings);
			if (isSuccess) { result.IncremenPassed(); } else { result.IncremenFailed(); }
		});

		Logger.Information("Tests Run Result> Passed: {PassedCount}, Failed: {FailedCount}.",
			result.PassedCount, result.FailedCount);

		if (result.FailedCount > 0)
		{
			throw new EasyFlowSqlException($"Unit test run failed. Failed test count {result.FailedCount} of {result.Total}.");
		}
	}

	private bool RunTest(TestItem test, string sqlConnectionString, EasyFlowSettings settings)
	{
		bool isSuccess = false;
		string? errorMessage = null;
		DateTime startedUtc = DateTime.UtcNow;

		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(settings.TestsTransactionIsolationLevel, _defaultTimeout);

			_da.ExecuteNonQuery(sqlConnectionString, test.FileData.Content);

			_transactionScope.Dispose(); //canceling transaction

			isSuccess = true;
			Logger.Information("PASSED Test: {filePath}", test.Name);
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			Logger.Error(ex, "FAILED Test: {filePath}. Error Message: {errorMessage}", test.Name, ex.Message);
		}

		DateTime completedUtc = DateTime.UtcNow;

		_da.SaveUnitTestResult(
			sqlConnectionString,
			test.FileData.RelativePath,
			test.FileData.Crc32Hash,
			startedUtc,
			(int)(completedUtc - startedUtc).TotalMilliseconds,
			isSuccess,
			errorMessage
		);

		return isSuccess;
	}
}
