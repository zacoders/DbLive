namespace DbLive.Deployers.Migrations;

public interface IMigrationVersionDeployer
{
	void DeployMigration(bool isSelfDeploy, Migration migration, DeployParameters parameters);
}