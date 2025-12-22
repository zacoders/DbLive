namespace EasyFlow.Deployers.Migrations;

public interface IMigrationsDeployer
{
	void DeployMigrations(bool isSelfDeploy, DeployParameters parameters);
}