namespace EasyFlow.Common;

public class EasyFlowProjectPath(string projectPath) : IEasyFlowProjectPath
{
	public string ProjectPath { get; } = projectPath;
}