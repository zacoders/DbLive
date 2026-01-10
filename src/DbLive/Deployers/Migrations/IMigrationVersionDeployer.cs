namespace DbLive.Deployers.Migrations;

public interface IMigrationVersionDeployer
{
	Task DeployAsync(Migration migration, DeployParameters parameters);
}