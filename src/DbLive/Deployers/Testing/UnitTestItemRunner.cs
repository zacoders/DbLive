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
		+ "for example a CATCH block with 'if @@trancount > 0 rollback' on SQL Server, or COMMIT in the test on PostgreSQL. "
		+ "DbLive rolled back what ran after it; what a COMMIT committed stays in the database. "
		+ "Code called inside a transaction must not end it: on an error it rolls back to a savepoint of its own (save transaction).";

	internal const string TransactionLeftOpenMessage =
		"The test left a transaction open: the test, its setup.test.sql or the code they call ran BEGIN TRANSACTION without COMMIT, "
		+ "for example a procedure that returns between them; called by an application, SQL Server fails such a procedure with Msg 266. "
		+ "DbLive rolled back the transaction of the test. Code that begins a transaction commits it on every path before it returns.";

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
		// setup.test.sql and the test get UnitTestItemTimeout each; their transaction twice that, so that it does not cut the two together.
		TransactionTimeout transactionTimeout = new(
			projectSettings.UnitTestItemTimeout * 2,
			"The transaction of the test",
			$"twice {nameof(DbLiveSettings.UnitTestItemTimeout)}"
		);
		try
		{
			long transactionStarted = Stopwatch.GetTimestamp();
			using TransactionScope _transactionScope = TransactionScopeManager.Create(testsIsolationLevel, transactionTimeout.Value);
			using Transaction transaction = Transaction.Current!.Clone();

			try
			{
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

				// the data access fails setup.test.sql and the test when they end the transaction or leave one open (the exceptions below).
				// This check covers a transaction that ended otherwise without failing them. An exception here (a broken connection,
				// a transaction that timed out) fails the test as well.
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
			catch (Exception ex) when (transactionTimeout.IsCauseOf(transaction, transactionStarted, ex))
			{
				throw transactionTimeout.Expired(ex);
			}
		}
		catch (DbLiveTransactionEndedException)
		{
			// the message of the data access speaks of a script; this one of the test, its setup.test.sql and the code they call,
			// as the check above.
			result.Output = TransactionEndedMessage;
			result.ErrorMessage = TransactionEndedMessage;
		}
		catch (DbLiveTransactionLeftOpenException)
		{
			result.Output = TransactionLeftOpenMessage;
			result.ErrorMessage = TransactionLeftOpenMessage;
		}
		catch (Exception ex)
		{
			// ErrorMessage is what [SqlFact] fails with and RunAllTestsAsync logs: it names setup.test.sql when that failed,
			// also when it ran out of time, otherwise the error reads as one of the test file.
			string errorMessage = test.SetupFileData is not null && !setupFileDeployed
				? $"Setup file error ({test.SetupFileData.RelativePath}): {ex.Message}"
				: ex.Message;

			result.ErrorMessage = errorMessage;
			result.Output = "ERROR: " + errorMessage + Environment.NewLine + result.Output;
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
