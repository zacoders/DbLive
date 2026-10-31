
namespace DbLive.Project;

internal static class MigrationFileNameParser
{
	public static MigrationItemInfo GetMigrationInfo(string filePath)
	{
		string fileName = Path.GetFileName(filePath);
		string fileExtension = Path.GetExtension(fileName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		string[] fileParts = fileNameWithoutExtension.Split('.');

		string migrationVersionStr = fileParts[0];
		string migrationFilePart2 = fileParts.Length > 1 ? fileParts[1] : "";
		string migrationFilePart3 = fileParts.Length > 2 ? fileParts[2] : "";
		string migrationFilePart4 = fileParts.Length > 3 ? fileParts[3] : "";

		MigrationItemType? migrationType = migrationFilePart2.ToLower() switch
		{
			"migration" => MigrationItemType.Migration,
			"m" => MigrationItemType.Migration,
			"undo" => MigrationItemType.Undo,
			"u" => MigrationItemType.Undo,
			"breaking" => MigrationItemType.Breaking,
			"b" => MigrationItemType.Breaking,
			_ => null
		};

		if (!long.TryParse(migrationVersionStr, out long version))
		{
			string errorMessage = "Version prefix failed to parse into a long integer.";
			throw new InvalidMigrationVersionException(fileName, errorMessage);
		}

		if (fileExtension == ".json")
		{
			// <version>.settings.json was one settings file for the whole version.
			if (IsSettingsPart(migrationFilePart2))
			{
				throw new ObsoleteMigrationSettingsFileException(fileName, migrationVersionStr);
			}

			if (!migrationType.HasValue || !IsSettingsPart(migrationFilePart3))
			{
				throw new UnknownMigrationSettingsException(fileName);
			}

			return new MigrationItemInfo()
			{
				Version = version,
				MigrationItemType = MigrationItemTypeMapper.GetSettingsType(migrationType.Value),
				Name = migrationFilePart4,
				FilePath = filePath
			};
		}

		if (migrationType.HasValue)
		{
			if (fileExtension != ".sql")
			{
				throw new InvalidMigrationItemTypeException(fileName, migrationType.Value, fileExtension, ".sql");
			}

			return new MigrationItemInfo()
			{
				Version = version,
				MigrationItemType = migrationType.Value,
				Name = migrationFilePart3,
				FilePath = filePath
			};
		}

		if (fileExtension == ".sql")
		{
			return new MigrationItemInfo()
			{
				Version = version,
				MigrationItemType = MigrationItemType.Migration,
				Name = migrationFilePart2,
				FilePath = filePath
			};
		}

		throw new UnknownMigrationItemTypeException(migrationFilePart2, fileName);
	}

	private static bool IsSettingsPart(string filePart)
		=> filePart.ToLower() is "settings" or "s";
}
