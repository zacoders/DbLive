
namespace DbLive.Project;

internal static class MigrationFileNameParser
{
	internal static MigrationItemInfo GetMigrationInfo(string filePath)
	{
		string fileName = Path.GetFileName(filePath);
		string fileExtension = Path.GetExtension(fileName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		string[] fileParts = fileNameWithoutExtension.Split('.');

		string migrationVersionStr = fileParts[0];
		string migrationFilePart2 = fileParts.Length > 1 ? fileParts[1] : "";
		string migrationFilePart3 = fileParts.Length > 2 ? fileParts[2] : "";

		if (!int.TryParse(migrationVersionStr, out var version))
		{
			throw new MigrationVersionParseException(fileName, migrationVersionStr);
		}

		MigrationItemType? migrationType = migrationFilePart2.ToLower() switch
		{
			"migration" => MigrationItemType.Migration,
			"m" => MigrationItemType.Migration,
			"undo" => MigrationItemType.Undo,
			"u" => MigrationItemType.Undo,
			"breaking" => MigrationItemType.Breaking,
			"b" => MigrationItemType.Breaking,
			"settings" => MigrationItemType.Settings,
			"s" => MigrationItemType.Settings,
			_ => null
		};

		if (migrationType.HasValue)
		{
			if (fileExtension != ".json" && migrationType == MigrationItemType.Settings)
			{
				throw new InvalidMigrationItemTypeException(fileName, migrationType.Value, fileExtension, ".json");
			}

			if (fileExtension != ".sql"
				&& migrationType.Value is MigrationItemType.Migration or MigrationItemType.Undo or MigrationItemType.Breaking)
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

		if (fileExtension == ".json")
		{
			throw new UnknownMigrationSettingsException(fileName);
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
}
