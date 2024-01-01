namespace EasyFlow;

public interface IEasyFlow
{
	void DeployProject(string projectPath, string sqlConnectionString, DeployParameters parameters);
}