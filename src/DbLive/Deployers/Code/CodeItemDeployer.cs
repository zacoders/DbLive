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
			CodeItemDto? codeItemDto = await _da.FindCodeItemAsync(codeItem.FileData.RelativePath).ConfigureAwait(false);

			if (codeItemDto != null
				&& codeItemDto.ContentHash == codeItem.FileData.ContentHash
				&& codeItemDto.Status == CodeItemStatus.Applied)
			{
				await _da.MarkCodeAsVerifiedAsync(codeItem.FileData.RelativePath, _timeProvider.UtcNow()).ConfigureAwait(false);
				_logger.Information("Deploying code file: {filePath} [hash match]", codeItem.FileData.RelativePath);
				return CodeItemDeployResult.Success();
			}

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
			//_logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.RelativePath);

			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - migrationStartedUtc).TotalMilliseconds;
			await _da.SaveCodeItemAsync(new CodeItemDto()
			{
				RelativePath = codeItem.FileData.RelativePath,
				Status = CodeItemStatus.Error,
				ContentHash = codeItem.FileData.ContentHash,
				AppliedUtc = appliedUtc,
				ExecutionTimeMs = executionTimeMs,
				CreatedUtc = _timeProvider.UtcNow(),
				ErrorMessage = ex.ToString()
			}).ConfigureAwait(false);
			return new CodeItemDeployResult
			{
				IsSuccess = false,
				Exception = new CodeDeploymentException($"Deploy code file error. File path: {codeItem.FileData.RelativePath}", ex)
			};
		}
	}
}
