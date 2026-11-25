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
			// one command without a transaction of its own, unlike a code file.
			await _da.ExecuteNonQueryAsync(
				item.FileData.Content,
				projectSettings.TransactionIsolationLevel,
				projectSettings.CodeItemTimeout
			).ConfigureAwait(false);

			DateTime appliedUtc = _timeProvider.UtcNow();

			await _da.SaveCodeItemAsync(new CodeItemDto
			{
				RelativePath = item.FileData.RelativePath,
				ItemType = CodeItemType.Breaking,
				Status = CodeItemStatus.Applied,
				ContentHash = item.FileData.ContentHash,
				AppliedUtc = appliedUtc,
				ExecutionTimeMs = (int)(appliedUtc - startedUtc).TotalMilliseconds,
				CreatedUtc = appliedUtc
			}).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// no row is written: the row of the script stays its last success, and the error is in the exception and the log. An error
			// of the write of the row after the script ran names the file too.
			CodeDeploymentException error = new(
				ex.WithSqlErrorOrMessage($"Deploy code breaking script error. File path: {item.FileData.RelativePath}"),
				ex
			);

			_logger.Error(error, "Deploy code breaking script error. File path: {FilePath}", item.FileData.RelativePath);

			throw error;
		}
	}
}
