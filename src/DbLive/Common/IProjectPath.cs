
namespace DbLive.Common;

public interface IProjectPath
{
	string Path { get; }
	string VisualStudioProjectPath { get; }
}


public class ProjectPath(string projectPath, string visualStudioProjectPath) : IProjectPath
{
	public string Path => projectPath;

	public string VisualStudioProjectPath => visualStudioProjectPath;
}

