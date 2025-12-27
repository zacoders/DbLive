namespace DbLive.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	void DeployMigrationItem(bool isSelfDeploy, int migrationVersion, MigrationItem migrationItem);
	void MarkAsSkipped(bool isSelfDeploy, int migrationVersion, MigrationItem migrationItem);
}