namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationItemMustExistsException(int migrationVersion)
	: Exception($"Migration item must exists. MigrationVersion='{migrationVersion}'.")
{
	internal int MigrationVersion { get; private set; } = migrationVersion;
}