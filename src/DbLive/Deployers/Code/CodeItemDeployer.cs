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
				CreatedUtc = _timeProvider.UtcNow(),
				ErrorMessage = ""
			}).ConfigureAwait(false);

			return CodeItemDeployResult.Success();
		}
		catch (Exception ex)
		{
			string errorMessage = $"Deploy code file error. File path: {codeItem.FileData.RelativePath}";

			// in a transaction the error row would never be seen: a failed deployment rolls it back, a successful retry overwrites it.
			// When the error ended the transaction, a retry cannot run in it: on PostgreSQL it fails, on SQL Server it would run
			// in autocommit mode and be committed with the files after it, while the deployment is rolled back.
			if (Transaction.Current is not null)
			{
				bool canRetry = await CanRetryInTransactionAsync(codeItem.FileData.RelativePath).ConfigureAwait(false);

				return new CodeItemDeployResult
				{
					IsSuccess = false,
					CanRetry = canRetry,
					Exception = new CodeDeploymentException(
						canRetry ? errorMessage : $"{errorMessage}. The error ended the deployment transaction, so the file is not retried.",
						ex
					)
				};
			}

			await SaveErrorRowAsync(codeItem, migrationStartedUtc, ex).ConfigureAwait(false);

			return CodeItemDeployResult.Failed(new CodeDeploymentException(errorMessage, ex));
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

	private async Task SaveErrorRowAsync(CodeItem codeItem, DateTime startedUtc, Exception error)
	{
		try
		{
			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - startedUtc).TotalMilliseconds;
			await _da.SaveCodeItemAsync(new CodeItemDto()
			{
				RelativePath = codeItem.FileData.RelativePath,
				ItemType = CodeItemType.Code,
				Status = CodeItemStatus.Error,
				ContentHash = codeItem.FileData.ContentHash,
				AppliedUtc = appliedUtc,
				ExecutionTimeMs = executionTimeMs,
				CreatedUtc = _timeProvider.UtcNow(),
				// the full exception with its stack traces is in the result: CodeDeployer logs it.
				ErrorMessage = error.GetSqlErrorOrMessage()
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// the server is unreachable, for one: the error of the file stays the result, with its path.
			_logger.Warning(ex, "The error row of {FilePath} is not saved.", codeItem.FileData.RelativePath);
		}
	}
}
