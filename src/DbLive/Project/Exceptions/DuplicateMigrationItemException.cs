namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class DuplicateMigrationItemException(long migrationVersion, MigrationItemType migrationItemType)
	: Exception($"Migration item already defined, check sql migrations in the project. MigrationVersion='{migrationVersion}', MigrationItemType={migrationItemType}.")
{
	internal long MigrationVersion { get; private set; } = migrationVersion;
	internal MigrationItemType MigrationItemType { get; private set; } = migrationItemType;
}