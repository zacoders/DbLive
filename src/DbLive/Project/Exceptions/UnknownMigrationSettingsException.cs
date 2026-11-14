namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class UnknownMigrationSettingsException : Exception
{
	internal UnknownMigrationSettingsException(string fileName)
		: base($"Unknown migration settings file {fileName}. Expected <version>.migration.settings.json or <version>.breaking.settings.json; short forms combine m or b with s, for example <version>.m.s.json.")
	{
	}
}
