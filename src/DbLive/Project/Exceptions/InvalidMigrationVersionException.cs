namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class InvalidMigrationVersionException : Exception
{
	internal InvalidMigrationVersionException(string fileName, string reason)
		: base($"Invalid migration version in file '{fileName}'. {reason}")
	{
		FileName = fileName;
	}

	public string FileName { get; private set; }
}
