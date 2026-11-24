namespace DbLive;

/// <summary>
/// The settings file of a migration or breaking script (<c>&lt;version&gt;.migration.settings.json</c>,
/// <c>&lt;version&gt;.breaking.settings.json</c>) has an unknown key, a wrong value (<c>null</c>, a <c>Timeout</c> that is not
/// <c>"hh:mm:ss"</c> or <c>"d.hh:mm:ss"</c>), invalid JSON, or <c>"TransactionWrapLevel": "Deployment"</c>.
/// Its keys are <c>TransactionWrapLevel</c>, <c>TransactionIsolationLevel</c> and <c>Timeout</c>. Nothing ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidMigrationItemSettingsException : Exception
{
	internal InvalidMigrationItemSettingsException(string relativePath, string error)
		: base($"Invalid migration settings file {relativePath}: {error}")
	{
		RelativePath = relativePath;
	}

	internal InvalidMigrationItemSettingsException(string relativePath, string error, Exception innerException)
		: base($"Invalid migration settings file {relativePath}: {error}", innerException)
	{
		RelativePath = relativePath;
	}

	/// <summary>
	/// The relative path of the settings file.
	/// </summary>
	public string RelativePath { get; private set; }
}
