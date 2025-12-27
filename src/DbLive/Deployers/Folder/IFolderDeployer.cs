namespace DbLive.Deployers.Folder;

public interface IFolderDeployer
{
	void DeployFolder(ProjectFolder projectFolder, DeployParameters parameters);
}