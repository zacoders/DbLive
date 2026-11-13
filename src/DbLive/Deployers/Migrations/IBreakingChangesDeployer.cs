namespace DbLive.Deployers.Migrations;

internal interface IBreakingChangesDeployer
{
	Task DeployAsync(ProjectSnapshot project, DeployParameters parameters);
}
