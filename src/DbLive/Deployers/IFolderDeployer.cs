
namespace DbLive.Deployers;

public interface IFolderDeployer
{
	void DeployFolder(ProjectFolder projectFolder, DeployParameters parameters);
}