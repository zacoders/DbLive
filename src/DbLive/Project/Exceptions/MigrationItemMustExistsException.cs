namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationItemMustExistsException(long migrationVersion)
	: Exception($"Migration item must exists. MigrationVersion='{migrationVersion}'.")
{
	internal long MigrationVersion { get; private set; } = migrationVersion;
}