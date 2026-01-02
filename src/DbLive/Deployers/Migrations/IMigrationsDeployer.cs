namespace DbLive.Deployers.Migrations;

public interface IMigrationsDeployer
{
	void Deploy(DeployParameters parameters);
}