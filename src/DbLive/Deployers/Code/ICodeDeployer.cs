namespace DbLive.Deployers.Code;

public interface ICodeDeployer
{
	void DeployCode(bool isSelfDeploy, DeployParameters parameters);
}