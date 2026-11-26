namespace DbLive;

/// <summary>
/// The project settings file (<c>settings.json</c>) has an unknown key, a wrong value or invalid JSON.
/// The inner exception is the <see cref="System.Text.Json.JsonException"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidProjectSettingsException : Exception
{
	internal InvalidProjectSettingsException(string relativePath, string error, Exception innerException)
		: base($"Invalid project settings file {relativePath}: {error}", innerException)
	{
		RelativePath = relativePath;
	}

	/// <summary>
	/// The relative path of the settings file: <c>settings.json</c>.
	/// </summary>
	public string RelativePath { get; private set; }
}
