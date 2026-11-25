
namespace DbLive.Deployers.Folder;

internal class FolderDeployer(
		ILogger _logger,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IFolderDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(FolderDeployer));

	public async Task DeployAsync(ProjectSnapshot project, ProjectFolder projectFolder)
	{
		_logger.Information("Deploying folder {ProjectFolder}.", projectFolder);

		IReadOnlyList<GenericItem> items = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => project.BeforeDeployItems,
			ProjectFolder.AfterDeploy => project.AfterDeployItems,
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolder.")
		};

		if (items.Count == 0)
		{
			_logger.Information("Folder {ProjectFolder} is empty.", projectFolder);
			return;
		}

		foreach (GenericItem item in items)
		{
			await DeployItemAsync(projectFolder, item).ConfigureAwait(false);
		}

		_logger.Debug("Deployment of the folder {ProjectFolder} successfully completed.", projectFolder);
	}

	private async Task DeployItemAsync(ProjectFolder projectFolder, GenericItem item)
	{
		_logger.Information("Deploying item: {RelativePath}", item.FileData.RelativePath);

		DateTime startedUtc = _timeProvider.UtcNow();

		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		TimeSpan timeout = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => projectSettings.BeforeDeployFolderTimeout,
			ProjectFolder.AfterDeploy => projectSettings.AfterDeployFolderTimeout,
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolder.")
		};

		try
		{
			await _da.ExecuteNonQueryAsync(
				item.FileData.Content,
				projectSettings.TransactionIsolationLevel,
				timeout
			).ConfigureAwait(false);

			DateTime completedUtc = _timeProvider.UtcNow();

			await _da.MarkItemAsAppliedAsync(
				projectFolder,
				item.FileData.RelativePath,
				startedUtc,
				completedUtc,
				(long)(completedUtc - startedUtc).TotalMilliseconds,
				item.FileData.ContentHash
			).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// SqlError is empty for an error that does not come from the server, such as a command timeout. An error of the write of
			// the row after the script ran is the exception of the provider: its message.
			string error = ex.GetSqlErrorOrMessage();

			_logger.Error(ex, "Error deploying {RelativePath}. SQL Error: {SqlError}", item.FileData.RelativePath, error);

			throw new FolderDeploymentException(item.FileData.RelativePath, error, ex);
		}
	}
}
