namespace DbLive.Deployers.Migrations;

internal interface IMigrationsDeployer
{
	Task DeployAsync(ProjectSnapshot project, DeployParameters parameters);
}
