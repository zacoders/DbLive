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
			DbLiveSettings settings = SettingsTools.GetSettings<DbLiveSettings>(settingsJson);
			CheckProjectIdLength(settings.ProjectId);
			return settings;
		}
		catch (JsonException e)
		{
			throw new InvalidProjectSettingsException(settingsPath, e.Message, e);
		}
	}

	/// <summary>
	/// Here and not in the check of the ProjectId, which runs after the database is created: an error of settings.json creates
	/// no database.
	/// </summary>
	/// <exception cref="JsonException"/>
	private static void CheckProjectIdLength(string? projectId)
	{
		if (projectId?.Length > ProjectIdTools.MaxLength)
		{
			const string path = $"$.{nameof(DbLiveSettings.ProjectId)}";
			throw new JsonException(
				$"The value of '{nameof(DbLiveSettings.ProjectId)}' cannot be longer than {ProjectIdTools.MaxLength} characters. Path: {path}.",
				path, null, null);
		}
	}
}
