namespace DbLive.Deployers;

public interface IDbLiveDeployer
{
	Task DeployAsync(DeployParameters parameters);
}