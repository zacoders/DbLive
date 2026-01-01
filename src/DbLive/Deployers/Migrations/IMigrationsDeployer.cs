namespace DbLive.Deployers.Migrations;

public interface IMigrationsDeployer
{
	void DeployMigrations(DeployParameters parameters);
}