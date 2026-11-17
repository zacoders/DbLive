namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class DuplicateMigrationItemException : Exception
{
	internal DuplicateMigrationItemException(long migrationVersion, MigrationItemType migrationItemType)
		: base($"Migration item already defined, check sql migrations in the project. MigrationVersion='{migrationVersion}', MigrationItemType={migrationItemType}.")
	{
		MigrationVersion = migrationVersion;
		MigrationItemType = migrationItemType;
	}

	internal long MigrationVersion { get; private set; }
	internal MigrationItemType MigrationItemType { get; private set; }
}
