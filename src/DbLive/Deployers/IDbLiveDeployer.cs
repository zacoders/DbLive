namespace DbLive.Deployers;

public interface IDbLiveDeployer
{
	void Deploy(bool isSelfDeploy, DeployParameters parameters);
}