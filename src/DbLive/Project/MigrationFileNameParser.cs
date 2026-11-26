using System.Globalization;

namespace DbLive.Project;

internal static class MigrationFileNameParser
{
	public static MigrationItemInfo GetMigrationInfo(string relativePath)
	{
		string fileName = Path.GetFileName(relativePath);
		string fileExtension = Path.GetExtension(fileName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		string[] fileParts = fileNameWithoutExtension.Split('.');

		string migrationVersionStr = GetVersionPart(relativePath);
		string migrationFilePart2 = fileParts.Length > 1 ? fileParts[1] : "";
		string migrationFilePart3 = fileParts.Length > 2 ? fileParts[2] : "";
		string migrationFilePart4 = fileParts.Length > 3 ? fileParts[3] : "";

		// DbLive has no undo scripts: an undo file left in the project would run as a migration named "undo"
		// and undo its own version, an undo settings file would be an unknown settings file.
		if (migrationFilePart2.ToLowerInvariant() is "undo" or "u")
		{
			throw new UndoNotSupportedException(relativePath);
		}

		MigrationItemType? migrationType = migrationFilePart2.ToLowerInvariant() switch
		{
			"migration" => MigrationItemType.Migration,
			"m" => MigrationItemType.Migration,
			"breaking" => MigrationItemType.Breaking,
			"b" => MigrationItemType.Breaking,
			_ => null
		};

		// digits only, in any culture: no sign, no spaces, no culture's minus sign.
		if (!long.TryParse(migrationVersionStr, NumberStyles.None, CultureInfo.InvariantCulture, out long version))
		{
			string errorMessage = migrationVersionStr.Length > 0 && migrationVersionStr.All(char.IsAsciiDigit)
				? $"The version '{migrationVersionStr}' is larger than {long.MaxValue.ToString(CultureInfo.InvariantCulture)}."
				: $"The version part '{migrationVersionStr}' must be digits only.";
			throw new InvalidMigrationVersionException(relativePath, errorMessage);
		}

		if (IsExtension(fileExtension, ".json"))
		{
			// <version>.settings.json was one settings file for the whole version.
			if (IsSettingsPart(migrationFilePart2))
			{
				throw new ObsoleteMigrationSettingsFileException(relativePath, migrationVersionStr);
			}

			if (!migrationType.HasValue || !IsSettingsPart(migrationFilePart3))
			{
				throw new UnknownMigrationSettingsException(relativePath);
			}

			return new MigrationItemInfo()
			{
				Version = version,
				MigrationItemType = MigrationItemTypeMapper.GetSettingsType(migrationType.Value),
				Name = migrationFilePart4,
				RelativePath = relativePath
			};
		}

		// a .sql file: only .sql and .json files reach the parser (GetMigrationsAsync reads only them, the providers embed only
		// SQL\**\*.sql). Without a type it is a migration, and its second part is the name: "003.create-users.sql".
		return new MigrationItemInfo()
		{
			Version = version,
			MigrationItemType = migrationType ?? MigrationItemType.Migration,
			Name = migrationType.HasValue ? migrationFilePart3 : migrationFilePart2,
			RelativePath = relativePath
		};
	}

	/// <summary>
	/// The version part of the file name as written, leading zeros included: the text before the first dot ("001" of "001.migration.sql").
	/// </summary>
	public static string GetVersionPart(string relativePath) => Path.GetFileName(relativePath).Split('.')[0];

	private static bool IsSettingsPart(string filePart)
		=> filePart.ToLowerInvariant() is "settings" or "s";

	/// <summary>
	/// Ignoring case, like the file patterns that found the file: "001.migration.SQL" is a migration.
	/// </summary>
	private static bool IsExtension(string fileExtension, string expectedExtension)
		=> fileExtension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase);
}
