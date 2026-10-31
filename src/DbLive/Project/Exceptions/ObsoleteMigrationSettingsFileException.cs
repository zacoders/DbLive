namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class ObsoleteMigrationSettingsFileException(string fileName, string version)
	: Exception($"Migration settings file {fileName} has the old name. Each script has its own settings file now: rename it to {version}.migration.settings.json (or {version}.m.s.json) for the migration script; the breaking and undo scripts take {version}.breaking.settings.json and {version}.undo.settings.json. The key MigrationTimeout is renamed to Timeout.")
{
	public string FileName { get; private set; } = fileName;
}
