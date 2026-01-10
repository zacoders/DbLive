namespace DbLive.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	Task DeployAsync(int migrationVersion, MigrationItem migrationItem);
}