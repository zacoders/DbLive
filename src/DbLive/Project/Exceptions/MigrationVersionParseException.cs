namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationVersionParseException : Exception
{
	public string FileName { get; private set; }
	public string MigrationVersionStr { get; private set; }

	public MigrationVersionParseException(string fileName, string migrationVersionStr)
		: base($"Cannot parse file '{fileName}', and version part '{migrationVersionStr}'.")
	{
		FileName = fileName;
		MigrationVersionStr = migrationVersionStr;
	}
}