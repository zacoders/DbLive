namespace DbLive.Project.Exceptions;


[ExcludeFromCodeCoverage]
public class MigrationSettingsWithoutScriptException(string relativePath, long migrationVersion, MigrationItemType scriptType)
	: Exception($"Settings file {relativePath} has no script: migration version {migrationVersion} has no {scriptType} item.")
{
	public string RelativePath { get; private set; } = relativePath;
	internal long MigrationVersion { get; private set; } = migrationVersion;
	internal MigrationItemType ScriptType { get; private set; } = scriptType;
}
