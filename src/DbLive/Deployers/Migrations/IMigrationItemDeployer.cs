namespace DbLive.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	void DeployMigrationItem(int migrationVersion, MigrationItem migrationItem);
	void MarkAsSkipped(int migrationVersion, MigrationItem migrationItem);
}