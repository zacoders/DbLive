namespace DbLive.Deployers.Code;

public interface ICodeDeployer
{
	Task DeployAsync(DeployParameters parameters);
}