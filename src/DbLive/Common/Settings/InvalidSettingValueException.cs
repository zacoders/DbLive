namespace DbLive.Common.Settings;

/// <summary>
/// The error of a value that a converter of the settings rejects: <c>The value of '&lt;key&gt;' &lt;error&gt;. Path: $.&lt;key&gt;.</c>
/// System.Text.Json sets the <see cref="JsonException.Path"/> when the exception leaves the converter, so the message names
/// the key, as the errors of <c>null</c> in <c>settings.json</c> do.
/// </summary>
internal sealed class InvalidSettingValueException(string error) : JsonException
{
	public override string Message => $"The value of '{Path?[(Path.LastIndexOf('.') + 1)..]}' {error}. Path: {Path}.";
}
