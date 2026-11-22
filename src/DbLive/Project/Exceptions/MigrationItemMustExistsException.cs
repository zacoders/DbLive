namespace DbLive;

/// <summary>
/// A version of the Migrations folder has a breaking script or a settings file but no migration script. To drop an old version of a
/// code object, use a breaking script of the Code folder instead. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MigrationItemMustExistsException : Exception
{
	internal MigrationItemMustExistsException(long migrationVersion)
		: base($"Migration item must exists. MigrationVersion='{migrationVersion}'.")
	{
		MigrationVersion = migrationVersion;
	}

	internal long MigrationVersion { get; private set; }
}
