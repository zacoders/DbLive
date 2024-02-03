namespace EasyFlow.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationExistsException : Exception
{
	internal Migration Migration { get; private set; }

	public MigrationExistsException(Migration migration)
		: base($"Migration alredy exists, duplicate folder name. Folder = '{migration.FolderPath.GetLastSegment()}'.")
	{
		Migration = migration;
	}
}