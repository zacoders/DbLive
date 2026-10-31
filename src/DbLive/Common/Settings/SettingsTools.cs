
namespace DbLive.Common.Settings;

internal static class SettingsTools
{
	public static JsonSerializerOptions JsonSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		ReadCommentHandling = JsonCommentHandling.Skip,
		Converters = { new JsonStringEnumConverter() }
	};

	public static T GetSettings<T>(string json) where T : new()
	{
		// todo: rewrite to throw exception with list of supported properties, analyze T fo find supported properties.
		if (string.IsNullOrWhiteSpace(json))
		{
			return new T();
		}
		T? settings = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
		return settings ?? new T();
	}

	/// <exception cref="InvalidMigrationItemSettingsException"/>
	public static MigrationItemSettings GetMigrationItemSettings(string json, string relativePath)
	{
		MigrationItemSettings settings;
		try
		{
			settings = GetSettings<MigrationItemSettings>(json);
		}
		catch (JsonException e)
		{
			throw new InvalidMigrationItemSettingsException(relativePath, e.Message, e);
		}

		if (settings.TransactionWrapLevel == TransactionWrapLevel.Deployment)
		{
			throw new InvalidMigrationItemSettingsException(
				relativePath,
				$"{nameof(TransactionWrapLevel)} '{TransactionWrapLevel.Deployment}' is a project setting (settings.json). A migration item takes '{TransactionWrapLevel.Migration}' or '{TransactionWrapLevel.None}'."
			);
		}

		return settings;
	}

	/// <summary>
	/// Settings of a migration, breaking or undo script from its settings file, empty when the file does not exist.
	/// </summary>
	/// <exception cref="InvalidMigrationItemSettingsException"/>
	public static MigrationItemSettings GetItemSettings(this Migration migration, MigrationItemType scriptType)
	{
		if (!migration.Items.TryGetValue(MigrationItemTypeMapper.GetSettingsType(scriptType), out MigrationItem? settingsItem))
		{
			return new MigrationItemSettings();
		}

		return GetMigrationItemSettings(settingsItem.FileData.Content, settingsItem.FileData.RelativePath);
	}
}
