namespace EasyFlow;

public interface IEasyFlow
{
	void DeployProject(string proejctPath, string sqlConnectionString, int maxVersion = int.MaxValue);
}