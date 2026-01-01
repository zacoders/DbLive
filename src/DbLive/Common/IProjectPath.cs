
namespace DbLive.Common;

public interface IProjectPath
{
	string Path { get; }
}


public class UserProjectPath(string projectPath): IProjectPath
{
	public string Path => projectPath;
}

public class InternalProjectPath(string projectPath): IProjectPath
{
	public string Path => projectPath;
}
