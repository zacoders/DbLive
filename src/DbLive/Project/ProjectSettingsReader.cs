namespace DbLive.Project;

public class SettingsAccessor(IScriptSource scriptSource) : ISettingsAccessor
{
	internal const string SettingsFileName = "settings.json";

	private readonly DbLiveSettings _defaultSettings = new();

	private Task<DbLiveSettings>? _settingsTask;
	private readonly object _settingsLock = new();

	public Task<DbLiveSettings> GetProjectSettingsAsync()
	{
		if (_settingsTask is not null) return _settingsTask;
		lock (_settingsLock)
		{
			return _settingsTask ??= LoadSettingsAsync();
		}
	}

	private async Task<DbLiveSettings> LoadSettingsAsync()
	{
		string? settingsPath = scriptSource.GetFiles()
			.FirstOrDefault(f => f.Equals(SettingsFileName, StringComparison.OrdinalIgnoreCase));

		if (settingsPath is not null)
		{
			string settingsJson = await scriptSource.ReadAsync(settingsPath).ConfigureAwait(false);

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
