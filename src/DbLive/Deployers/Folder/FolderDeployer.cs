
namespace DbLive.Deployers.Folder;

public class FolderDeployer(
		ILogger _logger,
		IDbLiveProject _project,
		IDbLiveDA _da,
		ITimeProvider _timeProvider,
		ISettingsAccessor _projectSettingsAccessor
	) : IFolderDeployer
{
	private readonly ILogger _logger = _logger.ForContext(typeof(FolderDeployer));

	public async Task DeployAsync(ProjectFolder projectFolder, DeployParameters parameters)
	{
		_logger.Information("Deploying folder {ProjectFolder}.", projectFolder);

		ReadOnlyCollection<GenericItem> items = await _project.GetFolderItemsAsync(projectFolder).ConfigureAwait(false);

		if (items.Count == 0)
		{
			_logger.Information("Folder {ProjectFolder} is empty.", projectFolder);
			return;
		}

		foreach (GenericItem item in items)
		{
			await DeployItemAsync(projectFolder, item).ConfigureAwait(false);
		}

		_logger.Debug("Deployment of the folder {ProjectFolder} successfully completed..", projectFolder);
	}

	private async Task DeployItemAsync(ProjectFolder projectFolder, GenericItem item)
	{
		_logger.Information("Deploying item: {filePath}", item.FileData.FileName);

		DateTime startedUtc = _timeProvider.UtcNow();

		DbLiveSettings projectSettings = await _projectSettingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		await _da.ExecuteNonQueryAsync(
			item.FileData.Content,
			projectSettings.TransactionIsolationLevel,
			projectFolder switch
			{
				ProjectFolder.BeforeDeploy => projectSettings.BeforeDeployFolderTimeout,
				ProjectFolder.AfterDeploy => projectSettings.AfterDeployFolderTimeout,
				_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
			}
		).ConfigureAwait(false);

		DateTime completedUtc = _timeProvider.UtcNow();

		await _da.MarkItemAsAppliedAsync(projectFolder, item.FileData.RelativePath, startedUtc, completedUtc, (long)(completedUtc - startedUtc).TotalMilliseconds).ConfigureAwait(false);
	}
}
