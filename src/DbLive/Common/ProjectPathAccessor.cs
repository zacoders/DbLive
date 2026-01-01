
namespace DbLive.Common;

public class ProjectPathAccessor(IProjectPath _projectPath, IFileSystem _fileSystem) 
	: IVsProjectPathAccessor
{
	private const string ProjectDirErrorDetails = "Make sure your sql project configured correctly. The projectdir.user file should be generated during the build. TODO: provide link to project configuration description.";
	public string VisualStudioProjectPath
	{
		get
		{
			// Note: projectdir.user file is needed to make it possible to open sql test file on sql test click in VS Test Explorer.
			string projectDirFile = _projectPath.Path.CombineWith("projectdir.user");
			if (!_fileSystem.FileExists(projectDirFile))
			{
				throw new Exception($"The projectdir.user file was not found. {ProjectDirErrorDetails}");
			}
			var lines = _fileSystem.FileReadAllLines(projectDirFile);
			if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
			{
				throw new Exception($"For some reason projectdir.user file is empty. {ProjectDirErrorDetails}");
			}
			string visualStudioProjectPath = lines[0];
			return visualStudioProjectPath;
		}
	}
}
