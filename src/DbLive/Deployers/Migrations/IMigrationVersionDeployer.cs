namespace DbLive.Deployers.Migrations;

public interface IMigrationVersionDeployer
{
	void Deploy(Migration migration, DeployParameters parameters);
}