namespace EasyFlow;

public interface IEasyFlowInternal
{
	void SelfDeployProjectInternal();
	void DeployProjectInternal(bool isSelfDeploy, DeployParameters parameters);
}