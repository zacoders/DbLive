namespace DbLive.Deployers.Testing;

public class UnitTestItemRunner(
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		IUnitTestResultChecker _unitTestResultChecker,
		ISettingsAccessor _projectSettingsAccessor
	) : IUnitTestItemRunner
{

	public async Task<TestRunResult> RunTestAsync(TestItem test)
	{
		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync();

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
				projectSettings.UnitTestItemTimeout
			);

			if (test.InitFileData is not null)
			{
				_da.ExecuteNonQuery(
					test.InitFileData.Content,
					TranIsolationLevel.Serializable,
					projectSettings.UnitTestItemTimeout
				);
			}

			List<SqlResult> resutls = _da.ExecuteQueryMultiple(
				test.FileData.Content,
				TranIsolationLevel.Serializable,
				projectSettings.UnitTestItemTimeout
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
