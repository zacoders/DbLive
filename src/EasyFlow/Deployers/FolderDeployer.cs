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
		if (!parameters.DeployCode)
		{
			return;
		}

		_logger.Information("Deploying folder {ProjectFolder}.", projectFolder);

		// todo: expected some permanent order, alpabetical.
		var items = _project.GetFolderItems(projectFolder);
		
		foreach ( var item in items ) {
			//TODO: Deploy item
			// Save deployment status to the table
		}

		_logger.Information("Deployment of the folder {ProjectFolder} successfully completed..", projectFolder);
	}
}
