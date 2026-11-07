namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknownMigrationSettingsException(string fileName)
	: Exception($"Unknown migration settings file {fileName}. Expected <version>.migration.settings.json or <version>.breaking.settings.json; short forms combine m or b with s, for example <version>.m.s.json.")
{
}
