namespace DbLive.Project;

public class SettingsAccessor(IProjectPathAccessor projectPath, IFileSystem _fileSystem)
	: ISettingsAccessor
{
	readonly string _projectPath = projectPath.ProjectPath;
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
				var settingsJson = _fileSystem.FileReadAllText(settingsPath);
				var loadedSettings = JsonConvert.DeserializeObject<DbLiveSettings>(settingsJson, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
				_settings = loadedSettings;
			}

			return _settings ?? _defaultSettings;
		}
	}
}