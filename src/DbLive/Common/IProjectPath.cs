
namespace DbLive.Common;

public interface IProjectPath
{
	/// <summary>
	/// Root of the scripts in <see cref="FileSystem"/>.
	/// </summary>
	string Path { get; }

	/// <summary>
	/// Reads the scripts: the resources of the SQL project assembly or a folder.
	/// </summary>
	IFileSystem FileSystem { get; }

	/// <summary>
	/// The scripts folder in the source project, used by "Go to source" of SQL tests. Null when unknown.
	/// </summary>
	string? VisualStudioProjectPath { get; }
}


public class ProjectPath(string projectPath, IFileSystem fileSystem, string? visualStudioProjectPath) : IProjectPath
{
	public string Path => projectPath;

	public IFileSystem FileSystem => fileSystem;

	public string? VisualStudioProjectPath => visualStudioProjectPath;
}
