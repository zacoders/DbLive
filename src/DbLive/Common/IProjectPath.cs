
namespace DbLive.Common;

public interface IProjectPath
{
	string Path { get; }
}


public class ProjectPath(string projectPath): IProjectPath
{
	public string Path => projectPath;
}

internal interface IInternalProjectPath : IProjectPath
{
}

public class InternalProjectPath(string projectPath): IInternalProjectPath
{
	public string Path => projectPath;
}
