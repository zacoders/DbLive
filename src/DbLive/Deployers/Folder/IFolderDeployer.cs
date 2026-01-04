namespace DbLive.Deployers.Folder;

public interface IFolderDeployer
{
	void Deploy(ProjectFolder projectFolder, DeployParameters parameters);
}