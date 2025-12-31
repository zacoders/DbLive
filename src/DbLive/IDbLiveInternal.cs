namespace DbLive;

public interface IDbLiveSelfDeployer
{
	void Deploy();
}

public interface IDbLiveInternalDeployer
{
	void Deploy(bool isSelfDeploy, DeployParameters parameters);
}