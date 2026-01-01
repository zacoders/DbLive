namespace DbLive.Deployers.Migrations;

internal interface ISelfMigrationsDeployer
{
	void DeployMigrations();
}