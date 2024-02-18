namespace EasyFlow.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	void DeployMigrationItem(bool isSelfDeploy, Migration migration, MigrationItem migrationItem);
	void MarkAsSkipped(bool isSelfDeploy, Migration migration, MigrationItem migrationItem);
}