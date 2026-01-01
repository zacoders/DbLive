namespace DbLive.Deployers.Testing;

public class UnitTestItemRunner(
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		IUnitTestResultChecker _unitTestResultChecker,
		ISettingsAccessor _projectSettingsAccessor
	) : IUnitTestItemRunner
{

	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

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
			using TransactionScope _transactionScope = TransactionScopeManager.Create(
				TranIsolationLevel.Serializable,
				_projectSettings.UnitTestItemTimeout
			);

			if (test.InitFileData is not null)
			{
				_da.ExecuteNonQuery(
					test.InitFileData.Content,
					TranIsolationLevel.Serializable,
					_projectSettings.UnitTestItemTimeout
				);
			}

			List<SqlResult> resutls = _da.ExecuteQueryMultiple(
				test.FileData.Content,
				TranIsolationLevel.Serializable,
				_projectSettings.UnitTestItemTimeout
			);

			ValidationResult compareResult = _unitTestResultChecker.ValidateTestResult(resutls);
			if (!compareResult.IsValid)
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
