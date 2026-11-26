namespace DbLive.Common.Settings;

/// <summary>
/// An enum key of <c>settings.json</c> and of the item settings files: a JSON string that is the name of a value, in any
/// case (<c>"None"</c>, <c>"none"</c>). Everything else is an error, the same on every target framework.
/// <see cref="JsonStringEnumConverter"/> takes more and keeps a value that the enum does not define (verified on net8.0,
/// net9.0 and net10.0): a number (<c>3</c>), a number in a string (<c>"3"</c>, on .NET 8 also with
/// <c>allowIntegerValues: false</c>) and names joined by a comma (<c>"Migration, None"</c> is 3, also for an enum without
/// <c>[Flags]</c>). <c>"TransactionWrapLevel": 3</c> ran every migration without a transaction. A defined number is an error
/// too: the enums are internal, so their numbers are not documented, and <c>2</c> meant as the second value is <c>None</c>.
/// </summary>
internal sealed class EnumSettingConverter : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

	private sealed class Converter<T> : JsonConverter<T> where T : struct, Enum
	{
		private static readonly string[] Names = Enum.GetNames<T>();

		/// <summary><c>must be "Deployment", "Migration" or "None"</c>.</summary>
		private static readonly string FormatError =
			$"must be {string.Join(", ", Names[..^1].Select(Quote))} or {Quote(Names[^1])}";

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// a key of settings.json. System.Text.Json does not pass null to the converter of a nullable key (the item settings),
			// it gives null, and SettingsTools.CheckNoNulls rejects it with the same message.
			if (reader.TokenType == JsonTokenType.Null)
			{
				throw new InvalidSettingValueException("cannot be null");
			}

			if (reader.TokenType == JsonTokenType.String && reader.GetString() is string text)
			{
				foreach (string name in Names)
				{
					if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
					{
						return Enum.Parse<T>(name);
					}
				}
			}

			throw new InvalidSettingValueException(FormatError);
		}

		// the settings are only read.
		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		private static string Quote(string name) => $"\"{name}\"";
	}
}
