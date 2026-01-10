namespace DbLive.Deployers.Folder;

public interface IFolderDeployer
{
	Task DeployAsync(ProjectFolder projectFolder, DeployParameters parameters);
}