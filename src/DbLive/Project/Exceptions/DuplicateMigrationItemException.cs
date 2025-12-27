namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class DuplicateMigrationItemException(int migrationVersion, MigrationItemType migrationItemType)
	: Exception($"Migration item already defined, check sql migrations in the project. MigrationVersion='{migrationVersion}', MigrationItemType={migrationItemType}.")
{
	internal int MigrationVersion { get; private set; } = migrationVersion;
	internal MigrationItemType MigrationItemType { get; private set; } = migrationItemType;
}