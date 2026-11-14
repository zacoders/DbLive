namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class InvalidMigrationVersionException : Exception
{
	internal InvalidMigrationVersionException(string fileName, string reason)
		: base($"Invalid migration version in file '{fileName}'. {reason}")
	{
		FileName = fileName;
		MigrationVersionStr = reason;
	}

	public string FileName { get; private set; }

	/// <summary>
	/// Why the version is invalid (the name is historical: it never held the version part).
	/// </summary>
	public string MigrationVersionStr { get; private set; }
}
