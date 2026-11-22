namespace DbLive;

/// <summary>
/// The Migrations folder has a settings file of the old format, one for the whole version: <c>&lt;version&gt;.settings.json</c>
/// or <c>&lt;version&gt;.s.json</c>. Each script has its own settings file now; the message names the new file. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ObsoleteMigrationSettingsFileException : Exception
{
	internal ObsoleteMigrationSettingsFileException(string fileName, string version)
		: base($"Migration settings file {fileName} has the old name. Each script has its own settings file now: rename it to {version}.migration.settings.json (or {version}.m.s.json) for the migration script; the breaking script takes {version}.breaking.settings.json. The key MigrationTimeout is renamed to Timeout.")
	{
		FileName = fileName;
	}

	/// <summary>
	/// The name of the file, without its folder.
	/// </summary>
	public string FileName { get; private set; }
}
