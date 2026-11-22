namespace DbLive;

/// <summary>
/// Two files of the Migrations folder have the same version and type, such as <c>005.migration.sql</c> and <c>005.m.sql</c>.
/// Nothing ran.
/// </summary>
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
