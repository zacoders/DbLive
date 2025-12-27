using DbLive.Adapter;

namespace DbLive.Deployers.Code;

public class CodeItemDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : ICodeItemDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(CodeItemDeployer));	
	private readonly DbLiveSettings _projectSettings = _projectSettingsAccessor.ProjectSettings;

	/// <inheritdoc/>
	public CodeItemDeployResult DeployCodeItem(bool isSelfDeploy, CodeItem codeItem)
	{
		DateTime migrationStartedUtc = _timeProvider.UtcNow();
		try
		{
			if (!isSelfDeploy)
			{
				CodeItemDto? codeItemDto = _da.FindCodeItem(codeItem.FileData.RelativePath);

				if (codeItemDto != null
					&& codeItemDto.ContentHash == codeItem.FileData.Crc32Hash
					&& codeItemDto.Status == CodeItemStatus.Applied)
				{
					_da.MarkCodeAsVerified(codeItem.FileData.RelativePath, _timeProvider.UtcNow());
					_logger.Information("Deploying code file: {filePath} [hash match]", codeItem.FileData.FileName);
					return CodeItemDeployResult.Success();
				}
			}

			_logger.Information("Deploying code file: {filePath}", codeItem.FileData.FileName);

			_da.ExecuteNonQuery(
				codeItem.FileData.Content,
				_projectSettings.TransactionIsolationLevel,
				_projectSettings.CodeItemTimeout
			);

			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - migrationStartedUtc).TotalMilliseconds;

			if (!isSelfDeploy)
			{
				_da.SaveCodeItem(new CodeItemDto()
				{
					RelativePath = codeItem.FileData.RelativePath,
					Status = CodeItemStatus.Applied,
					ContentHash = codeItem.FileData.Crc32Hash,
					AppliedUtc = appliedUtc,
					ExecutionTimeMs = executionTimeMs,
					CreatedUtc = _timeProvider.UtcNow(),
					ErrorMessage = ""
				});
			}

			return CodeItemDeployResult.Success();
		}
		catch (Exception ex)
		{
			//_logger.Error(ex, "Deploy code file error. File path: {filePath}", codeItem.FileData.RelativePath);

			DateTime appliedUtc = _timeProvider.UtcNow();
			int executionTimeMs = (int)(appliedUtc - migrationStartedUtc).TotalMilliseconds;
			_da.SaveCodeItem(new CodeItemDto()
			{
				RelativePath = codeItem.FileData.RelativePath,
				Status = CodeItemStatus.Error,
				ContentHash = codeItem.FileData.Crc32Hash,
				AppliedUtc = appliedUtc,
				ExecutionTimeMs = executionTimeMs,
				CreatedUtc = _timeProvider.UtcNow(),
				ErrorMessage = ex.ToString()
			});
			return new CodeItemDeployResult { 
				IsSuccess = false, 
				Exception = new CodeDeploymentException($"Deploy code file error. File path: {codeItem.FileData.RelativePath}", ex) 
			};
		}
	}
}
