namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class MigrationItemMustExistsException : Exception
{
	internal MigrationItemMustExistsException(long migrationVersion)
		: base($"Migration item must exists. MigrationVersion='{migrationVersion}'.")
	{
		MigrationVersion = migrationVersion;
	}

	internal long MigrationVersion { get; private set; }
}
