namespace DbLive.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	void Deploy(int migrationVersion, MigrationItem migrationItem);
}