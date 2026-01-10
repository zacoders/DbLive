namespace DbLive.Project;

public class SettingsAccessor(IProjectPath projectPath, IFileSystem fileSystem) : ISettingsAccessor
{
	private readonly string _projectPath = projectPath.Path;
	private readonly IFileSystem _fileSystem = fileSystem;

	private readonly DbLiveSettings _defaultSettings = new();

	private Task<DbLiveSettings>? _settingsTask;

	public Task<DbLiveSettings> GetProjectSettingsAsync()
	{
		return _settingsTask ??= LoadSettingsAsync();
	}

	private async Task<DbLiveSettings> LoadSettingsAsync()
	{
		string settingsPath = _projectPath.CombineWith("settings.json");

		if (_fileSystem.FileExists(settingsPath))
		{
			string settingsJson = await _fileSystem.FileReadAllTextAsync(settingsPath);

			DbLiveSettings? settings = JsonSerializer.Deserialize<DbLiveSettings>(
				settingsJson,
				SettingsTools.JsonSerializerOptions);

			if (settings is not null)
			{
				return settings;
			}
		}

		return _defaultSettings;
	}
}
