namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class InvalidMigrationItemSettingsException : Exception
{
	public InvalidMigrationItemSettingsException(string relativePath, string error)
		: base($"Invalid migration settings file {relativePath}: {error}")
	{
		RelativePath = relativePath;
	}

	public InvalidMigrationItemSettingsException(string relativePath, string error, Exception innerException)
		: base($"Invalid migration settings file {relativePath}: {error}", innerException)
	{
		RelativePath = relativePath;
	}

	public string RelativePath { get; private set; }
}
