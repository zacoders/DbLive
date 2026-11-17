namespace DbLive.Deployers.Folder;

internal interface IFolderDeployer
{
	Task DeployAsync(ProjectSnapshot project, ProjectFolder projectFolder, DeployParameters parameters);
}
