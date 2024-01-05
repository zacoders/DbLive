namespace EasyFlow.Project;


[ExcludeFromCodeCoverage]
[Serializable]
public class MigrationTaskExistsException : Exception
{
	internal MigrationItem MigrationTask { get; private set; }

	public MigrationTaskExistsException(MigrationItem migrationTask)
		: base($"Migration task alredy exists, duplicate file name. File = '{migrationTask.FileData.FilePath.GetLastSegment()}'.")
	{
		MigrationTask = migrationTask;
	}
}