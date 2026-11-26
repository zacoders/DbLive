namespace DbLive;

/// <summary>
/// A version of the Migrations folder has a breaking script or a settings file but no migration script. To drop an old version of a
/// code object, use a breaking script of the Code folder instead. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MigrationItemMustExistsException : Exception
{
	internal MigrationItemMustExistsException(long migrationVersion, IReadOnlyList<string> relativePaths)
		: base(
			$"Migration version {migrationVersion} has no migration script, only "
			+ string.Join(", ", relativePaths.Select(p => $"'{p}'"))
			+ ". Add the migration script of this version, or fix the version in the names of these files. "
			+ "To drop an old version of a code object, use a breaking script of the Code folder instead."
		)
	{
		MigrationVersion = migrationVersion;
		RelativePaths = relativePaths;
	}

	/// <summary>
	/// The relative paths of the files of the version: its breaking script and settings files.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }

	internal long MigrationVersion { get; private set; }
}
