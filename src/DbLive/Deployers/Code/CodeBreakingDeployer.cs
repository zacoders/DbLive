namespace DbLive.Deployers.Code;

/// <summary>
/// Breaking scripts of the Code folder have no state: each one runs on every breaking deployment, like AfterDeploy runs on every
/// deployment. A hash-based "applied once" would skip the script after a later release (git revert) brought the old object back.
/// </summary>
internal class CodeBreakingDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : ICodeBreakingDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(CodeBreakingDeployer));

	/// <inheritdoc/>
	public async Task DeployAsync(ProjectSnapshot project)
	{
		IReadOnlyList<GenericItem> items = project.CodeBreakingItems;

		if (items.Count == 0)
		{
			_logger.Information("No code breaking scripts.");
			return;
		}

		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		foreach (GenericItem item in items)
		{
			await DeployItemAsync(item, projectSettings).ConfigureAwait(false);
		}
	}

	private async Task DeployItemAsync(GenericItem item, DbLiveSettings projectSettings)
	{
		_logger.Information("Deploying code breaking script: {FilePath}", item.FileData.RelativePath);

		DateTime startedUtc = _timeProvider.UtcNow();

		try
		{
			// one command without its own transaction, like a code file.
			await _da.ExecuteNonQueryAsync(
				item.FileData.Content,
				projectSettings.TransactionIsolationLevel,
				projectSettings.CodeItemTimeout
			).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Deploy code breaking script error. File path: {FilePath}", item.FileData.RelativePath);

			// in a transaction the error row would never be seen: the failed deployment rolls it back. When the error ended the transaction,
			// it cannot be written there either: on PostgreSQL the write fails, on SQL Server it would be committed outside the transaction.
			if (Transaction.Current is null)
			{
				await _da.SaveCodeItemAsync(CreateRow(item, CodeItemStatus.Error, startedUtc, ex.GetSqlErrorOrMessage())).ConfigureAwait(false);
			}

			throw new CodeDeploymentException($"Deploy code breaking script error. File path: {item.FileData.RelativePath}", ex);
		}

		await _da.SaveCodeItemAsync(CreateRow(item, CodeItemStatus.Applied, startedUtc, "")).ConfigureAwait(false);
	}

	private CodeItemDto CreateRow(GenericItem item, CodeItemStatus status, DateTime startedUtc, string errorMessage)
	{
		DateTime appliedUtc = _timeProvider.UtcNow();

		return new CodeItemDto
		{
			RelativePath = item.FileData.RelativePath,
			ItemType = CodeItemType.Breaking,
			Status = status,
			ContentHash = item.FileData.ContentHash,
			AppliedUtc = appliedUtc,
			ExecutionTimeMs = (int)(appliedUtc - startedUtc).TotalMilliseconds,
			CreatedUtc = appliedUtc,
			ErrorMessage = errorMessage
		};
	}
}
