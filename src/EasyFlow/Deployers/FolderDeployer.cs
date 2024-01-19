using EasyFlow.Adapter;

namespace EasyFlow.Deployers;

public class FolderDeployer(
		ILogger logger,
		IEasyFlowProject _project,
		IEasyFlowDA _da,
		ITimeProvider _timeProvider
	)
{
	private readonly ILogger _logger = logger.ForContext(typeof(FolderDeployer));

	public void DeployFolder(ProjectFolder projectFolder, DeployParameters parameters)
	{
		_logger.Information("Deploying folder {ProjectFolder}.", projectFolder);

		ReadOnlyCollection<GenericItem> items = _project.GetFolderItems(projectFolder);

		if (items.Count == 0)
		{
			_logger.Information("Folder {ProjectFolder} is empty.", projectFolder);
		}

		foreach ( var item in items ) {
			//TODO: Deploy item
			// Save deployment status to the table
		}

		_logger.Information("Deployment of the folder {ProjectFolder} successfully completed..", projectFolder);
	}
}
