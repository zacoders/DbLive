namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationExistsException(Migration migration)
	: Exception($"Migration already exists, duplicate folder name. Folder = '{migration.FolderPath.GetLastSegment()}'.")
{
	internal Migration Migration { get; private set; } = migration;
}