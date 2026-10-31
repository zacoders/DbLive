namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class UnknownMigrationSettingsException(string fileName)
	: Exception($"Unknown migration settings file {fileName}. Expected <version>.migration.settings.json, <version>.breaking.settings.json or <version>.undo.settings.json; short forms combine m, b, u with s, for example <version>.m.s.json.")
{
}
