
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

	public static MigrationSettings GetMigrationSettings(this DbLiveSettings projectSettings, MigrationSettings migrationSettings)
	{
		return new MigrationSettings
		{
			TransactionWrapLevel = migrationSettings.TransactionWrapLevel ?? projectSettings.TransactionWrapLevel,
			TransactionIsolationLevel = migrationSettings.TransactionIsolationLevel ?? projectSettings.TransactionIsolationLevel,
			MigrationTimeout = migrationSettings.MigrationTimeout ?? projectSettings.MigrationTimeout
		};
	}
}
