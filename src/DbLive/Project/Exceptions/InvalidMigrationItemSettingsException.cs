namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class InvalidMigrationItemSettingsException : Exception
{
	internal InvalidMigrationItemSettingsException(string relativePath, string error)
		: base($"Invalid migration settings file {relativePath}: {error}")
	{
		RelativePath = relativePath;
	}

	internal InvalidMigrationItemSettingsException(string relativePath, string error, Exception innerException)
		: base($"Invalid migration settings file {relativePath}: {error}", innerException)
	{
		RelativePath = relativePath;
	}

	public string RelativePath { get; private set; }
}
