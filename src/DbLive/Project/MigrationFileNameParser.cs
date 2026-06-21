
namespace DbLive.Project;

internal class MigrationFileNameParser(IMigrationVersionValidator migrationVersionValidator) : IMigrationFileNameParser
{
	public MigrationItemInfo GetMigrationInfo(string filePath, bool validateVersion)
	{
		string fileName = Path.GetFileName(filePath);
		string fileExtension = Path.GetExtension(fileName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		string[] fileParts = fileNameWithoutExtension.Split('.');

		string migrationVersionStr = fileParts[0];
		string migrationFilePart2 = fileParts.Length > 1 ? fileParts[1] : "";
		string migrationFilePart3 = fileParts.Length > 2 ? fileParts[2] : "";

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


		// 4. Final conversion to long for database storage
		if (!long.TryParse(migrationVersionStr, out long version))
		{
			string errorMessage = "Version prefix failed to parse into a long integer.";
			throw new InvalidMigrationVersionException(fileName, errorMessage);
		}

		if (validateVersion)
		{
			migrationVersionValidator.Validate(version, fileName);
		}

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
