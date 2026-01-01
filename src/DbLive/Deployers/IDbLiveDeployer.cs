namespace DbLive.Deployers;

public interface IDbLiveDeployer
{
	void Deploy(DeployParameters parameters);
}