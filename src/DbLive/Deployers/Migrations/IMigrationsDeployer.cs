namespace DbLive.Deployers.Migrations;

public interface IMigrationsDeployer
{
	Task DeployAsync(DeployParameters parameters);
}