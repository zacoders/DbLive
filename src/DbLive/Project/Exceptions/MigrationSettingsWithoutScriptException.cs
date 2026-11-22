namespace DbLive;

/// <summary>
/// A settings file of the Migrations folder has no script of its type: a <c>&lt;version&gt;.breaking.settings.json</c> without
/// <c>&lt;version&gt;.breaking.sql</c>. Nothing ran.
/// </summary>
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

	/// <summary>
	/// The relative path of the settings file.
	/// </summary>
	public string RelativePath { get; private set; }
	internal long MigrationVersion { get; private set; }
	internal MigrationItemType ScriptType { get; private set; }
}
