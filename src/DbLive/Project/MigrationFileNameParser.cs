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

		/* Attempt to parse major version */
		if (!int.TryParse(migrationVersionStr, out int version))
		{
			throw new MigrationVersionParseException(fileName, migrationVersionStr);
		}

		/* Logic to handle: {version}.{subversion}.{type}.{name}.sql */
		int subVersionId = 0;
		string migrationTypePart = "";
		string migrationName = "";

		if (fileParts.Length > 2)
		{
			/* Case with explicit SubVersionId: 001.12345.migration.name.sql */
			if (int.TryParse(fileParts[1], out int parsedSubVersion))
			{
				subVersionId = parsedSubVersion;
				migrationTypePart = fileParts[2];
				migrationName = fileParts.Length > 3 ? fileParts[3] : "";
			}
			else
			{
				/* Fallback if second part is not a number: 001.migration.name.sql */
				migrationTypePart = fileParts[1];
				migrationName = fileParts[2];
			}
		}
		else if (fileParts.Length == 2)
		{
			/* Simple case: 001.migration.sql or 001.name.sql */
			migrationTypePart = fileParts[1];
		}

		MigrationItemType? migrationType = migrationTypePart.ToLower() switch
		{
			"migration" or "m" => MigrationItemType.Migration,
			"undo" or "u" => MigrationItemType.Undo,
			"breaking" or "b" => MigrationItemType.Breaking,
			"settings" or "s" => MigrationItemType.Settings,
			_ => null
		};

		if (migrationType.HasValue)
		{
			ValidateExtension(fileName, migrationType.Value, fileExtension);

			return new MigrationItemInfo()
			{
				Version = version,
				SubVersionId = subVersionId,
				MigrationItemType = migrationType.Value,
				Name = migrationName,
				FilePath = filePath
			};
		}

		/* Default handling for .sql files without explicit type part */
		if (fileExtension == ".sql")
		{
			return new MigrationItemInfo()
			{
				Version = version,
				SubVersionId = subVersionId,
				MigrationItemType = MigrationItemType.Migration,
				Name = migrationTypePart, // In this case part 2 is the name
				FilePath = filePath
			};
		}

		if (fileExtension == ".json")
		{
			throw new UnknownMigrationSettingsException(fileName);
		}

		throw new UnknownMigrationItemTypeException(migrationTypePart, fileName);
	}

	private static void ValidateExtension(string fileName, MigrationItemType type, string ext)
	{
		if (ext != ".json" && type == MigrationItemType.Settings)
		{
			throw new InvalidMigrationItemTypeException(fileName, type, ext, ".json");
		}

		if (ext != ".sql" && type is MigrationItemType.Migration or MigrationItemType.Undo
			or MigrationItemType.Breaking)
		{
			throw new InvalidMigrationItemTypeException(fileName, type, ext, ".sql");
		}
	}
}