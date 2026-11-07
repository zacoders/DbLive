namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class InvalidMigrationVersionException(string fileName, string reason)
	: Exception($"Invalid migration version in file '{fileName}'. {reason}")
{
	public string FileName { get; private set; } = fileName;

	/// <summary>
	/// Why the version is invalid (the name is historical: it never held the version part).
	/// </summary>
	public string MigrationVersionStr { get; private set; } = reason;
}