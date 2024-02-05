namespace EasyFlow.Common;

public class ProjectPathAccessor(IProjectPath projectPath, IFileSystem _fileSystem) : IProjectPathAccessor
{
	private readonly string _projectPath = projectPath.Path;

	public string ProjectPath 
	{ 
		get
		{
			if (!_fileSystem.PathExistsAndNotEmpty(_projectPath))
			{
				throw new ProjectFolderIsEmptyException(_projectPath);
			}
			return _projectPath;
		}
	}
}

public interface IProjectPath
{
	string Path { get; }
}

public class ProjectPath (string projectPath) : IProjectPath
{
	public string Path => projectPath;
}
