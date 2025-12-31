namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnexpectedUndoMigrationItemException(int migrationVersion, string undoRelativePath)
	: Exception($"UNDO cannot be defined without migration {undoRelativePath}. MigrationVersion='{migrationVersion}'.")
{
	internal int MigrationVersion { get; private set; } = migrationVersion;
	internal string UndoRelativePath { get; private set; } = undoRelativePath;
}