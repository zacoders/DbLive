
namespace DbLive.Project;

public class SettingsAccessor(IProjectPathAccessor projectPath, IFileSystem _fileSystem)
	: ISettingsAccessor
{
	readonly string _projectPath = projectPath.ProjectPath;
	private readonly DbLiveSettings _defaultSettings = new();
	private DbLiveSettings? _settings;

	private static JsonSerializerOptions JsonSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		ReadCommentHandling = JsonCommentHandling.Skip,
		Converters = { new JsonStringEnumConverter() }
	};

	public DbLiveSettings ProjectSettings
	{
		get
		{
			if (_settings is not null) return _settings;

			string settingsPath = _projectPath.CombineWith("settings.json");
			if (_fileSystem.FileExists(settingsPath))
			{
				string settingsJson = _fileSystem.FileReadAllText(settingsPath);
				_settings = JsonSerializer.Deserialize<DbLiveSettings>(settingsJson, JsonSerializerOptions);
				_settings ??= _defaultSettings;
			}
			else
			{
				_settings = _defaultSettings;
			}

			return _settings;
		}
	}
}