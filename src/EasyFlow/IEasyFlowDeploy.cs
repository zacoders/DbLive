namespace EasyFlow;

public interface IEasyFlowDeploy
{
	void DeployProject(string proejctPath, string sqlConnectionString);
}