
namespace DbLive.Project;


public sealed class InternalDbLiveProject(
		InternalProjectPath projectPath,
		IFileSystem _fileSystem,
		ISettingsAccessor _settingsAccessor,
		IProjectPathAccessor vsProjectPathAccessor
	) : DbLiveProjectBase(projectPath, _fileSystem, _settingsAccessor, vsProjectPathAccessor), 
		IDbLiveProjectBase
{
}
