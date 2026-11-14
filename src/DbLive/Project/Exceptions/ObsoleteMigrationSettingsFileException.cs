namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class ObsoleteMigrationSettingsFileException : Exception
{
	internal ObsoleteMigrationSettingsFileException(string fileName, string version)
		: base($"Migration settings file {fileName} has the old name. Each script has its own settings file now: rename it to {version}.migration.settings.json (or {version}.m.s.json) for the migration script; the breaking script takes {version}.breaking.settings.json. The key MigrationTimeout is renamed to Timeout.")
	{
		FileName = fileName;
	}

	public string FileName { get; private set; }
}
