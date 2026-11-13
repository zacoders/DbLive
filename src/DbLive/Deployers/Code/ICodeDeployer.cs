namespace DbLive.Deployers.Code;

internal interface ICodeDeployer
{
	Task DeployAsync(ProjectSnapshot project, DeployParameters parameters);
}
