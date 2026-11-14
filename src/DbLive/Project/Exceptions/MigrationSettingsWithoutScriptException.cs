namespace DbLive;


[ExcludeFromCodeCoverage]
public sealed class MigrationSettingsWithoutScriptException : Exception
{
	internal MigrationSettingsWithoutScriptException(string relativePath, long migrationVersion, MigrationItemType scriptType)
		: base($"Settings file {relativePath} has no script: migration version {migrationVersion} has no {scriptType} item.")
	{
		RelativePath = relativePath;
		MigrationVersion = migrationVersion;
		ScriptType = scriptType;
	}

	public string RelativePath { get; private set; }
	internal long MigrationVersion { get; private set; }
	internal MigrationItemType ScriptType { get; private set; }
}
