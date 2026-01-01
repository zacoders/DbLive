namespace DbLive.Deployers.Migrations;

public interface IMigrationVersionDeployer
{
	void DeployMigration(Migration migration, DeployParameters parameters);
}