using System.Globalization;

namespace DbLive.Common.Settings;

/// <summary>
/// A time span of <c>settings.json</c> and of the item settings files: <c>"hh:mm:ss"</c> or <c>"d.hh:mm:ss"</c>, with an
/// optional fraction of a second (<c>"00:00:00.5"</c>), not negative. The same on every target framework: the converter of
/// System.Text.Json is the one of the runtime, and .NET 9 and later also read the short forms of the format <c>c</c>
/// (<c>"60"</c> as 60 days, <c>"02:00"</c> as 2 hours), which .NET 8 rejects. Here they are errors on every runtime:
/// <c>"60"</c> meant as seconds would be 60 days.
/// </summary>
internal sealed class TimeSpanSettingConverter : JsonConverter<TimeSpan>
{
	internal const string FormatError = "must be a time span \"hh:mm:ss\" or \"d.hh:mm:ss\", such as \"00:00:30\" or \"1.00:00:00\"";

	public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		// a TimeSpan? key (the Timeout of the item settings) never gets here with null: SettingsTools.CheckNoNulls rejects it.
		if (reader.TokenType == JsonTokenType.Null)
		{
			throw new InvalidTimeSpanException("cannot be null");
		}

		// the format c with both colons: [-][d.]hh:mm:ss[.fffffff], without the short forms "d", "hh:mm" and "d.hh:mm".
		if (reader.TokenType != JsonTokenType.String
			|| reader.GetString() is not string text
			|| text.Count(c => c == ':') != 2
			|| !TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out TimeSpan value))
		{
			throw new InvalidTimeSpanException(FormatError);
		}

		if (value < TimeSpan.Zero)
		{
			throw new InvalidTimeSpanException("cannot be negative");
		}

		return value;
	}

	// the settings are only read.
	public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
		throw new NotSupportedException();

	/// <summary>
	/// System.Text.Json sets the <see cref="JsonException.Path"/> when the exception leaves the converter, so the message names
	/// the key, as the errors of <c>null</c> in <c>settings.json</c> do.
	/// </summary>
	private sealed class InvalidTimeSpanException(string error) : JsonException
	{
		public override string Message => $"The value of '{Path?[(Path.LastIndexOf('.') + 1)..]}' {error}. Path: {Path}.";
	}
}
