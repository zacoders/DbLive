namespace DbLive.Deployers.Testing;

internal class UnitTestItemRunner(
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		IUnitTestResultChecker _unitTestResultChecker,
		ISettingsAccessor _projectSettingsAccessor
	) : IUnitTestItemRunner
{
	internal const string TransactionEndedMessage =
		"The transaction of the test ended before the test did: the test, its setup.test.sql or the code they call ran ROLLBACK or COMMIT, "
		+ "for example a CATCH block with 'if @@trancount > 0 rollback'. The statements after it ran in autocommit mode and stay in the database. "
		+ "Code called inside a transaction must not end it: on an error it rolls back to a savepoint of its own (save transaction).";

	public async Task<TestRunResult> RunTestAsync(TestItem test)
	{
		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		TestRunResult result = new()
		{
			IsSuccess = false,
			StartedUtc = _timeProvider.UtcNow()
		};

		bool setupFileDeployed = false;
		IStopWatch stopWatch = _timeProvider.StartNewStopwatch();
		TranIsolationLevel testsIsolationLevel = projectSettings.TestsTransactionIsolationLevel;
		try
		{
			using TransactionScope _transactionScope = TransactionScopeManager.Create(
				testsIsolationLevel,
				projectSettings.UnitTestItemTimeout
			);

			if (test.SetupFileData is not null)
			{
				await _da.ExecuteNonQueryAsync(
					test.SetupFileData.Content,
					testsIsolationLevel,
					projectSettings.UnitTestItemTimeout
				).ConfigureAwait(false);
				setupFileDeployed = true;
			}

			List<SqlResult> resutls = await _da.ExecuteQueryMultipleAsync(
				test.FileData.Content,
				testsIsolationLevel,
				projectSettings.UnitTestItemTimeout
			).ConfigureAwait(false);

			// on SQL Server the code under test can end the transaction of the test, and the statements after it are committed at once.
			// Transaction.Current stays Active then, only the server knows. One check after the test covers setup.test.sql too: whatever
			// runs after the end of the transaction runs in autocommit mode, the test script included. An exception here (a broken
			// connection, a transaction that timed out) fails the test as well.
			if (!await _da.IsTransactionUsableAsync().ConfigureAwait(false))
			{
				result.Output = TransactionEndedMessage;
				result.ErrorMessage = TransactionEndedMessage;
				return result;
			}

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
			if (test.SetupFileData is not null && !setupFileDeployed)
			{
				result.ErrorMessage = ex.Message;
				result.Output = "ERROR: Setup file error. " + ex.Message + Environment.NewLine + result.Output;
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
