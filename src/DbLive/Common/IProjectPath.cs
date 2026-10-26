
namespace DbLive.Common;

public interface IProjectPath
{
	string Path { get; }
	string VisualStudioProjectPath { get; }
}


public class ProjectPath(string projectPath, Func<string> visualStudioProjectPath) : IProjectPath
{
	public ProjectPath(string projectPath, string visualStudioProjectPath)
		: this(projectPath, () => visualStudioProjectPath)
	{
	}

	public string Path => projectPath;

	public string VisualStudioProjectPath => visualStudioProjectPath();
}

