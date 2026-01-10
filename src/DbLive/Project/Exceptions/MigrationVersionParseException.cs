namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationVersionParseException(string fileName, string migrationVersionStr)
	: Exception($"Cannot parse file '{fileName}', and version part '{migrationVersionStr}'.")
{
	public string FileName { get; private set; } = fileName;
	public string MigrationVersionStr { get; private set; } = migrationVersionStr;
}