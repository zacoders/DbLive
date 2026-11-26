namespace DbLive;

/// <summary>
/// Two files of the Migrations folder have the same version and type, such as <c>005.migration.sql</c> and <c>005.m.sql</c>.
/// Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DuplicateMigrationItemException : Exception
{
	internal DuplicateMigrationItemException(
		long migrationVersion,
		MigrationItemType migrationItemType,
		string relativePath,
		string otherRelativePath
	)
		: base(
			$"Files '{relativePath}' and '{otherRelativePath}' are both the {migrationItemType} item of migration version {migrationVersion}: "
			+ "a version has one file of each type. Rename or delete one of them."
		)
	{
		MigrationVersion = migrationVersion;
		MigrationItemType = migrationItemType;
		RelativePaths = [relativePath, otherRelativePath];
	}

	/// <summary>
	/// The relative paths of the two files.
	/// </summary>
	public IReadOnlyList<string> RelativePaths { get; private set; }

	internal long MigrationVersion { get; private set; }
	internal MigrationItemType MigrationItemType { get; private set; }
}
