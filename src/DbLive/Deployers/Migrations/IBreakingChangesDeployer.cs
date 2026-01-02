namespace DbLive.Deployers.Migrations;

public interface IBreakingChangesDeployer
{
	void Deploy(DeployParameters parameters);
}