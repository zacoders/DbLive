namespace EasyFlow.Deployers.Code;

public interface ICodeDeployer
{
	void DeployCode(bool isSelfDeploy, DeployParameters parameters);
}