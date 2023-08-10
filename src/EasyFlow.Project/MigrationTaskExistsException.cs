namespace EasyFlow.Project;

[Serializable]
public class MigrationTaskExistsException : Exception
{
	internal MigrationTask MigrationTask { get; private set; }

	public MigrationTaskExistsException(MigrationTask migrationTask)
		: base($"Migration task alredy exists, duplicate file name. File = '{migrationTask.FileUri.GetLastSegment()}'.")
	{
		MigrationTask = migrationTask;
	}
}