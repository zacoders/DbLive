
namespace DbLive.Project;

public sealed class UserDbLiveProject(
		UserProjectPath projectPath,
		IFileSystem _fileSystem,
		ISettingsAccessor _settingsAccessor,
		IProjectPathAccessor vsProjectPathAccessor
	) : DbLiveProjectBase(projectPath, _fileSystem, _settingsAccessor, vsProjectPathAccessor),
		IDbLiveProjectBase
{
}
