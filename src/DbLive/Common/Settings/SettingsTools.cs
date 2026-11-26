
namespace DbLive.Common.Settings;

internal static class SettingsTools
{
	public static JsonSerializerOptions JsonSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		ReadCommentHandling = JsonCommentHandling.Skip,
		Converters = { new EnumSettingConverter(), new TimeSpanSettingConverter() }
	};

	/// <summary>
	/// The settings of <c>settings.json</c> or of an item settings file, the defaults for an empty file.
	/// </summary>
	/// <exception cref="JsonException"/>
	public static T GetSettings<T>(string json) where T : new()
	{
		// todo: rewrite to throw exception with list of supported properties, analyze T fo find supported properties.
		if (string.IsNullOrWhiteSpace(json))
		{
			return new T();
		}

		// first the errors of System.Text.Json: invalid JSON and unknown keys.
		T? settings = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
		CheckNoNulls(json);
		return settings!;
	}

	private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip };

	/// <summary>
	/// <c>null</c> is an error for every key, for an item of a list and for the whole file: a key is left out to keep its
	/// default. System.Text.Json gives <c>null</c> for a string, a list, a nullable key (the item settings) and an item of a
	/// list, as if the key were missing; for a number or <c>true</c>/<c>false</c> it fails with an error of its own, for an enum
	/// and a time span the converter of DbLive does (<see cref="EnumSettingConverter"/>, <see cref="TimeSpanSettingConverter"/>).
	/// Without the check a <c>null</c> folder failed the load later with a NullReferenceException that named neither the file
	/// nor the key, and a <c>null</c> item of TestFilePatterns matched every file. RespectNullableAnnotations is not on net8.0
	/// and does not check the items of a list. The settings have no nested objects, so the keys and the items of the lists are
	/// all there is to check, and a new key needs no line here.
	/// </summary>
	/// <exception cref="JsonException"/>
	private static void CheckNoNulls(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
		JsonElement root = document.RootElement;

		if (root.ValueKind == JsonValueKind.Null)
		{
			throw new JsonException("The file cannot be null. Path: $.", "$", null, null);
		}

		foreach (JsonProperty property in root.EnumerateObject())
		{
			string path = $"$.{property.Name}";

			if (property.Value.ValueKind == JsonValueKind.Null)
			{
				throw new JsonException($"The value of '{property.Name}' cannot be null. Path: {path}.", path, null, null);
			}

			if (property.Value.ValueKind == JsonValueKind.Array)
			{
				int index = 0;
				foreach (JsonElement item in property.Value.EnumerateArray())
				{
					if (item.ValueKind == JsonValueKind.Null)
					{
						string itemPath = $"{path}[{index}]";
						throw new JsonException($"An item of '{property.Name}' cannot be null. Path: {itemPath}.", itemPath, null, null);
					}
					index++;
				}
			}
		}
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
	/// Settings of a migration or breaking script from its settings file, empty when the file does not exist.
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
