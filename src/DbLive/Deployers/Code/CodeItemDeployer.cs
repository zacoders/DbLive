namespace DbLive.Deployers.Code;

public class CodeItemDeployer(
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

			await _da.ExecuteNonQueryAsync(
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
			CodeDeploymentException error = new($"Deploy code file error. File path: {codeItem.FileData.RelativePath}", ex);

			// in a transaction the error row would never be seen: a failed deployment rolls it back, a successful retry overwrites it.
			// It cannot be written there either when the error aborted the transaction (PostgreSQL), and neither can a retry run.
			if (Transaction.Current is not null)
			{
				return new CodeItemDeployResult
				{
					IsSuccess = false,
					CanRetry = !_da.ErrorAbortsTransaction,
					Exception = error
				};
			}

			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - migrationStartedUtc).TotalMilliseconds;
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
				ErrorMessage = ex.GetSqlErrorOrMessage()
			}).ConfigureAwait(false);

			return CodeItemDeployResult.Failed(error);
		}
	}
}
