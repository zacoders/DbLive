namespace DbLive.Deployers.Code;

internal class CodeItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : ICodeItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(CodeItemDeployer));

	/// <inheritdoc/>
	public async Task<CodeItemDeployResult> DeployAsync(CodeItem codeItem)
	{
		DbLiveSettings _projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);
		DateTime migrationStartedUtc = _timeProvider.UtcNow();
		try
		{
			_logger.Information("Deploying code file: {filePath}", codeItem.FileData.RelativePath);

			// it is applied in part when a later batch fails: the log says why the file is not applied as a whole.
			if (Transaction.Current is null && _da.RunsCodeItemWithoutTransaction(codeItem.FileData.Content))
			{
				_logger.Information(
					"Code file {filePath} runs without a transaction of its own: it has DDL that the server does not allow in a transaction (In-Memory OLTP on SQL Server).",
					codeItem.FileData.RelativePath
				);
			}

			await _da.ExecuteCodeItemAsync(
				codeItem.FileData.Content,
				_projectSettings.TransactionIsolationLevel,
				_projectSettings.CodeItemTimeout
			).ConfigureAwait(false);

			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - migrationStartedUtc).TotalMilliseconds;

			await _da.SaveCodeItemAsync(new CodeItemDto()
			{
				RelativePath = codeItem.FileData.RelativePath,
				ItemType = CodeItemType.Code,
				Status = CodeItemStatus.Applied,
				ContentHash = codeItem.FileData.ContentHash,
				AppliedUtc = appliedUtc,
				ExecutionTimeMs = executionTimeMs,
				CreatedUtc = _timeProvider.UtcNow()
			}).ConfigureAwait(false);

			return CodeItemDeployResult.Success();
		}
		catch (Exception ex)
		{
			// no row is written: the error is in the exception and the log. The file runs again on the next deployment, whatever its
			// content: CodeDeployer marked its applied row applying before the run, and a file without an applied row runs anyway.
			string errorMessage = $"Deploy code file error. File path: {codeItem.FileData.RelativePath}";

			// a file that ended the transaction it ran in with COMMIT or ROLLBACK ends it on every run, and a file that left a transaction
			// open leaves it open on every run. In the deployment transaction nothing is left to retry it in (the data access rolled back
			// the transaction that the file left open); in a transaction of its own a retry would run on top of what its COMMIT committed,
			// or fail the same way. A file that failed inside a transaction it began (SQL Server) left the deployment transaction usable,
			// but the data access rolled it back with the transaction of the file: it is not the error that ended it.
			string? notRetried = ex switch
			{
				DbLiveTransactionEndedException when Transaction.Current is not null => "The file ended the deployment transaction",
				DbLiveTransactionEndedException => "The file ended its transaction",
				DbLiveTransactionLeftOpenException => "The file left a transaction open",
				DbLiveSqlException { RolledBackWithTransactionLeftOpen: true } when Transaction.Current is not null =>
					"The file failed inside a transaction that it began, and DbLive rolled back the deployment transaction with it",
				_ => null
			};

			bool canRetry;

			if (notRetried is not null)
			{
				canRetry = false;
				errorMessage = $"{errorMessage}. {notRetried}, so it is not retried.";
			}
			else if (Transaction.Current is not null)
			{
				// when the error ended the transaction, a retry cannot run in it: on PostgreSQL it fails, on SQL Server it would run
				// in autocommit mode and be committed with the files after it, while the deployment is rolled back.
				canRetry = await CanRetryInTransactionAsync(codeItem.FileData.RelativePath).ConfigureAwait(false);

				errorMessage = canRetry ? errorMessage : $"{errorMessage}. The error ended the deployment transaction, so the file is not retried.";
			}
			else
			{
				canRetry = true;
			}

			return new CodeItemDeployResult
			{
				IsSuccess = false,
				CanRetry = canRetry,
				Exception = new CodeDeploymentException(ex.WithSqlErrorOrMessage(errorMessage), ex)
			};
		}
	}

	/// <summary>
	/// After a file failed in the deployment transaction: true when the transaction is still usable, so the file can be retried in it.
	/// An exception here means it is not: the error broke the connection, or the transaction timed out, and the connection opened for
	/// the check cannot enlist in it. That exception must not replace the error of the file, which is the result.
	/// </summary>
	private async Task<bool> CanRetryInTransactionAsync(string relativePath)
	{
		try
		{
			return await _da.IsTransactionUsableAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.Warning(ex, "The deployment transaction cannot be used after the error of {FilePath}.", relativePath);
			return false;
		}
	}
}
