namespace DbLive.Project;

internal class SettingsAccessor(IScriptSource scriptSource) : ISettingsAccessor
{
	internal const string SettingsFileName = "settings.json";

	private readonly DbLiveSettings _defaultSettings = new();

	private Task<DbLiveSettings>? _settingsTask;
	private readonly object _settingsLock = new();

	public async Task<DbLiveSettings> GetProjectSettingsAsync()
	{
		Task<DbLiveSettings> load;
		lock (_settingsLock)
		{
			load = _settingsTask ??= LoadSettingsAsync();
		}

		try
		{
			return await load.ConfigureAwait(false);
		}
		catch
		{
			// a failed read is not kept, so the next deployment from this builder reads settings.json again (a file locked
			// for a moment by an antivirus).
			lock (_settingsLock)
			{
				if (_settingsTask == load)
				{
					_settingsTask = null;
				}
			}
			throw;
		}
	}

	/// <exception cref="InvalidProjectSettingsException"/>
	private async Task<DbLiveSettings> LoadSettingsAsync()
	{
		string? settingsPath = scriptSource.GetFiles()
			.FirstOrDefault(f => f.Equals(SettingsFileName, StringComparison.OrdinalIgnoreCase));

		if (settingsPath is null)
		{
			return _defaultSettings;
		}

		string settingsJson = await scriptSource.ReadAsync(settingsPath).ConfigureAwait(false);

		try
		{
			// an empty file gives the defaults, as an empty item settings file does.
			return SettingsTools.GetSettings<DbLiveSettings>(settingsJson);
		}
		catch (JsonException e)
		{
			throw new InvalidProjectSettingsException(settingsPath, e.Message, e);
		}
	}
}
