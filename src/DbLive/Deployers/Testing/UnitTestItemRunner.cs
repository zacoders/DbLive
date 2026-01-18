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
		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		TestRunResult result = new()
		{
			IsSuccess = false,
			StartedUtc = _timeProvider.UtcNow()
		};

		bool initFileDeployed = false;
		IStopWatch stopWatch = _timeProvider.StartNewStopwatch();
		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(
				TranIsolationLevel.Serializable,
				projectSettings.UnitTestItemTimeout
			);

			if (test.InitFileData is not null)
			{
				await _da.ExecuteNonQueryAsync(
					test.InitFileData.Content,
					TranIsolationLevel.Serializable,
					projectSettings.UnitTestItemTimeout
				).ConfigureAwait(false);
				initFileDeployed = true;
			}

			List<SqlResult> resutls = await _da.ExecuteQueryMultipleAsync(
				test.FileData.Content,
				TranIsolationLevel.Serializable,
				projectSettings.UnitTestItemTimeout
			).ConfigureAwait(false);

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
			if (test.InitFileData is not null && !initFileDeployed)
			{
				result.ErrorMessage = ex.Message;
				result.Output = "ERROR: Init file deployment error. " + ex.Message + Environment.NewLine + result.Output;
				result.Exception = ex;
			}
			else
			{
				result.ErrorMessage = ex.Message;
				result.Output = "ERROR:" + ex.Message + Environment.NewLine + result.Output;
				result.Exception = ex;
			}
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
