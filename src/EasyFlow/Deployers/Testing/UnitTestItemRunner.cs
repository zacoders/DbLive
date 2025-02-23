using EasyFlow.Adapter;

namespace EasyFlow.Deployers.Testing;

public class UnitTestItemRunner(
	IEasyFlowDA _da,
	ITimeProvider _timeProvider,
	IUnitTestResultChecker _unitTestResultChecker
) : IUnitTestItemRunner
{
	//TODO: timeout should be configurable.
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30); // test should be fast by default

	public TestRunResult RunTest(TestItem test)
	{
		TestRunResult result = new()
		{
			IsSuccess = false,
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

			MultipleResults resutls = _da.ExecuteQueryMultiple(test.FileData.Content);

			ValidationResult compareResult = _unitTestResultChecker.ValidateTestResult(resutls);
			if (compareResult.CompareResult == CompareResult.Mismatch)
			{
				result.Output = compareResult.Output;
				result.ErrorMessage = compareResult.Output;
				result.IsSuccess = false;
			}
			else
			{
				result.IsSuccess = true;
				result.Output = "Success";
			}
			
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
