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
			CheckNoNulls(settings);
			return settings;
		}
		catch (JsonException e)
		{
			throw new InvalidProjectSettingsException(settingsPath, e.Message, e);
		}
	}

	/// <summary>
	/// System.Text.Json takes <c>null</c> for a string or a list, and for an item of a list: net8.0 has no
	/// RespectNullableAnnotations, and it does not check the items of a list on any target. The project load would fail later
	/// with a NullReferenceException that names neither the file nor the key, and a null item of TestFilePatterns would match
	/// every file. A new key of a reference type needs a line here.
	/// </summary>
	/// <exception cref="JsonException"/>
	private static void CheckNoNulls(DbLiveSettings settings)
	{
		CheckNotNull(settings.MigrationsFolder, nameof(DbLiveSettings.MigrationsFolder));
		CheckNotNull(settings.CodeFolder, nameof(DbLiveSettings.CodeFolder));
		CheckNotNull(settings.TestsFolder, nameof(DbLiveSettings.TestsFolder));
		CheckNotNull(settings.BeforeDeployFolder, nameof(DbLiveSettings.BeforeDeployFolder));
		CheckNotNull(settings.AfterDeployFolder, nameof(DbLiveSettings.AfterDeployFolder));
		CheckNoNullItems(settings.TestFilePatterns, nameof(DbLiveSettings.TestFilePatterns));
		CheckNoNullItems(settings.CodeSubFoldersDeploymentOrder, nameof(DbLiveSettings.CodeSubFoldersDeploymentOrder));
	}

	private static void CheckNotNull(object? value, string key)
	{
		if (value is null)
		{
			string path = $"$.{key}";
			throw new JsonException($"The value of '{key}' cannot be null. Path: {path}.", path, null, null);
		}
	}

	private static void CheckNoNullItems(IEnumerable<string?>? items, string key)
	{
		CheckNotNull(items, key);

		int index = 0;
		foreach (string? item in items!)
		{
			if (item is null)
			{
				string path = $"$.{key}[{index}]";
				throw new JsonException($"An item of '{key}' cannot be null. Path: {path}.", path, null, null);
			}
			index++;
		}
	}
}
