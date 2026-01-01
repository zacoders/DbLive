
namespace DbLive.Project;

public class SettingsAccessor(IProjectPath projectPath, IFileSystem _fileSystem)
	: ISettingsAccessor
{
	readonly string _projectPath = projectPath.Path;
	private readonly DbLiveSettings _defaultSettings = new();
	private DbLiveSettings? _settings;

	public DbLiveSettings ProjectSettings
	{
		get
		{
			if (_settings is not null) return _settings;

			string settingsPath = _projectPath.CombineWith("settings.json");
			if (_fileSystem.FileExists(settingsPath))
			{
				string settingsJson = _fileSystem.FileReadAllText(settingsPath);
				_settings = JsonSerializer.Deserialize<DbLiveSettings>(settingsJson, SettingsTools.JsonSerializerOptions);
			}

			_settings ??= _defaultSettings;

			return _settings;
		}
	}
}