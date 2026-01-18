using System.Reflection;

namespace DbLive.Common;

// todo: completely remove IVsProjectPathAccessor, it is not longer needed, replaced with IProjectPath.VisualStudioProjectPath
public class ProjectPathAccessor(IProjectPath projectPath, IFileSystem fileSystem)
	: IVsProjectPathAccessor
{
	//private const string ProjectDirErrorDetails =
	//	"Make sure your sql project configured correctly. The projectdir.user file should be generated during the build. TODO: provide link to project configuration description.";

	//private readonly IProjectPath _projectPath = projectPath;
	private readonly IFileSystem _fileSystem = fileSystem;

	//private Task<string>? _visualStudioProjectPathTask;

	public Task<string> GetVisualStudioProjectPathAsync()
	{
		//return _visualStudioProjectPathTask ??= LoadVisualStudioProjectPathAsync();
		return Task.FromResult(projectPath.VisualStudioProjectPath);
	}

	//private async Task<string> LoadVisualStudioProjectPathAsync()
	//{
	//	// Note: projectdir.user file is needed to make it possible to open sql test file on sql test click in VS Test Explorer.
	//	string projectDirFile = _projectPath.Path.CombineWith("projectdir.user");

	//	if (!_fileSystem.FileExists(projectDirFile))
	//	{
	//		throw new Exception($"The projectdir.user file was not found. {ProjectDirErrorDetails}");
	//	}

	//	var lines = await _fileSystem.FileReadAllLinesAsync(projectDirFile).ConfigureAwait(false);

	//	if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
	//	{
	//		throw new Exception($"For some reason projectdir.user file is empty. {ProjectDirErrorDetails}");
	//	}

	//	string visualStudioProjectPath = lines[0];
	//	return visualStudioProjectPath;
	//}
}
