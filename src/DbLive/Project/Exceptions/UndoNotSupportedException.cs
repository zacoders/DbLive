namespace DbLive.Project.Exceptions;

/// <summary>
/// A file of the Migrations folder is an undo script or its settings file (<c>005.undo.sql</c>, <c>005.u.sql</c>,
/// <c>005.undo.settings.json</c>). DbLive has no undo scripts: run as a migration, the file would undo its own version.
/// </summary>
[ExcludeFromCodeCoverage]
public class UndoNotSupportedException(string relativePath)
	: Exception(
		$"{relativePath} is an undo script. DbLive does not support undo scripts and does not roll back a database: delete the file. "
		+ "To roll back a release, roll back the application and keep the database before the breaking phase runs, "
		+ "and fix a wrong migration with a new migration (see \"Roll back a release\" in the DbLive docs)."
	)
{
	public string RelativePath { get; private set; } = relativePath;
}
