namespace DbLive;

public interface IDbLiveInternal
{
	void SelfDeployProjectInternal();
	void DeployProjectInternal(bool isSelfDeploy, DeployParameters parameters);
}