namespace DbLive.Project;

internal static class MigrationItemTypeMapper
{
	// values of dblive.migration.item_type, they follow the file names.
	private static readonly Dictionary<MigrationItemType, string> _dbValues = new()
	{
		{ MigrationItemType.Migration, "migration" },
		{ MigrationItemType.Undo, "undo" },
		{ MigrationItemType.Breaking, "breaking" },
		{ MigrationItemType.MigrationSettings, "migration.settings" },
		{ MigrationItemType.BreakingSettings, "breaking.settings" },
		{ MigrationItemType.UndoSettings, "undo.settings" }
	};

	private static readonly Dictionary<string, MigrationItemType> _itemTypes =
		_dbValues.ToDictionary(i => i.Value, i => i.Key);

	private static readonly Dictionary<MigrationItemType, MigrationItemType> _settingsTypes = new()
	{
		{ MigrationItemType.Migration, MigrationItemType.MigrationSettings },
		{ MigrationItemType.Breaking, MigrationItemType.BreakingSettings },
		{ MigrationItemType.Undo, MigrationItemType.UndoSettings }
	};

	private static readonly Dictionary<MigrationItemType, MigrationItemType> _scriptTypes =
		_settingsTypes.ToDictionary(i => i.Value, i => i.Key);

	public static string ToDbValue(MigrationItemType itemType)
		=> _dbValues.TryGetValue(itemType, out string? dbValue)
			? dbValue
			: throw new ArgumentOutOfRangeException(nameof(itemType), itemType, "Unknown migration item type.");

	public static MigrationItemType FromDbValue(string dbValue)
		=> _itemTypes.TryGetValue(dbValue, out MigrationItemType itemType)
			? itemType
			: throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, "Unknown value of dblive.migration.item_type.");

	/// <summary>
	/// The settings item of a script item: <see cref="MigrationItemType.Breaking"/> -> <see cref="MigrationItemType.BreakingSettings"/>.
	/// </summary>
	public static MigrationItemType GetSettingsType(MigrationItemType scriptType)
		=> _settingsTypes.TryGetValue(scriptType, out MigrationItemType settingsType)
			? settingsType
			: throw new ArgumentOutOfRangeException(nameof(scriptType), scriptType, "Not a script item type.");

	public static bool IsSettings(MigrationItemType itemType) => _scriptTypes.ContainsKey(itemType);

	/// <summary>
	/// The script item a settings item belongs to: <see cref="MigrationItemType.BreakingSettings"/> -> <see cref="MigrationItemType.Breaking"/>.
	/// </summary>
	public static MigrationItemType GetScriptType(MigrationItemType settingsType)
		=> _scriptTypes.TryGetValue(settingsType, out MigrationItemType scriptType)
			? scriptType
			: throw new ArgumentOutOfRangeException(nameof(settingsType), settingsType, "Not a settings item type.");
}
