namespace DbLive.Deployers.Migrations;

public interface IMigrationItemDeployer
{
	Task DeployAsync(long migrationVersion, MigrationItem migrationItem);
}