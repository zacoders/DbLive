namespace DbLive.Deployers.Migrations;

public interface IBreakingChangesDeployer
{
	void DeployBreakingChanges(DeployParameters parameters);
}