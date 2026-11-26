namespace DbLive;

/// <summary>
/// Applied migration or breaking scripts were edited: their content no longer matches the checksum stored when they were applied.
/// The message lists the files. Revert the edits and add a new migration, or deploy with
/// <see cref="DeployParameters.RepairMigrationChecksums"/> once the database already matches the files.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MigrationChecksumMismatchException : Exception
{
	internal MigrationChecksumMismatchException(string errorMessage)
		: base(errorMessage)
	{
	}
}
