namespace DbLive;

/// <summary>
/// A <c>.json</c> file of the Migrations folder is not a settings file of a script: expected
/// <c>&lt;version&gt;.migration.settings.json</c> or <c>&lt;version&gt;.breaking.settings.json</c>, short <c>&lt;version&gt;.m.s.json</c>
/// and <c>&lt;version&gt;.b.s.json</c>. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UnknownMigrationSettingsException : Exception
{
	internal UnknownMigrationSettingsException(string fileName)
		: base($"Unknown migration settings file {fileName}. Expected <version>.migration.settings.json or <version>.breaking.settings.json; short forms combine m or b with s, for example <version>.m.s.json.")
	{
	}
}
