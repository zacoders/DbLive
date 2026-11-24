using System.Data.SqlTypes;
using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;

namespace DbLive.Deployers.Testing;

/// <summary>
/// The text by which <c>rows</c> compares a value, and which its failure message prints: the JSON of System.Text.Json,
/// except for the values whose default JSON does not show the value (<see cref="TextConverterFactory"/>).
/// </summary>
internal static class RowValueText
{
	// NaN and infinity as the strings "NaN", "Infinity" and "-Infinity", also inside an array: by default System.Text.Json throws on
	// them. NaN then equals NaN, and in rows equals the text 'NaN' too, as rows compares no types.
	private static readonly JsonSerializerOptions Options = new()
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
		PropertyNamingPolicy = null,
		WriteIndented = false,
		NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
		// The failure message prints the text: an apostrophe and non-ASCII letters as they are, not as '.
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		Converters = { new TextConverterFactory() }
	};

	/// <summary>
	/// The text of the value, or null when the value cannot be compared; then <paramref name="error"/> says why.
	/// </summary>
	public static string? Write(object? value, out string? error)
	{
		error = null;
		try
		{
			return JsonSerializer.Serialize(value, Options);
		}
		catch (Exception e)
		{
			// A property getter that throws, or a type without properties and without ToString() (TextConverter).
			error = e.GetBaseException().Message;
			return null;
		}
	}

	private static bool OverridesToString(Type type) =>
		type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType is Type declaringType
		&& declaringType != typeof(object)
		&& declaringType != typeof(ValueType);

	/// <summary>
	/// Writes as text the values whose default JSON does not show the value:
	/// <list type="bullet">
	/// <item><see cref="DBNull"/> as <c>null</c>. By default it is <c>{}</c>, so NULL equaled every value written as <c>{}</c>.</item>
	/// <item>A type without public properties by its <c>ToString()</c>: PostgreSQL <c>macaddr</c> and <c>macaddr8</c>
	/// (<c>PhysicalAddress</c>), <c>pg_lsn</c> (<c>NpgsqlLogSequenceNumber</c>). By default all its values are <c>{}</c> and equal.</item>
	/// <item><see cref="IPAddress"/> by its <c>ToString()</c>. By default its getters throw. PostgreSQL <c>inet</c> does not come as one:
	/// <c>PostgreSqlDA</c> reads it as text with the netmask, which <see cref="IPAddress"/> does not have.</item>
	/// <item>A SQL Server CLR type (<c>geography</c>, <c>geometry</c>, <c>hierarchyid</c>: <see cref="INullable"/>) by its <c>ToString()</c>,
	/// the text that <c>.ToString()</c> returns in T-SQL. By default every <c>hierarchyid</c> is <c>{"IsNull":false}</c>, and <c>geography</c> throws.</item>
	/// </list>
	/// </summary>
	private sealed class TextConverterFactory : JsonConverterFactory
	{
		// The options without this factory: how System.Text.Json writes a type by default.
		private static readonly JsonSerializerOptions DefaultOptions = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert == typeof(DBNull)
			|| typeof(IPAddress).IsAssignableFrom(typeToConvert)
			|| (typeof(INullable).IsAssignableFrom(typeToConvert) && OverridesToString(typeToConvert))
			|| HasNoProperties(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			(JsonConverter)Activator.CreateInstance(typeof(TextConverter<>).MakeGenericType(typeToConvert))!;

		private static bool HasNoProperties(Type type)
		{
			try
			{
				return DefaultOptions.GetTypeInfo(type) is { Kind: JsonTypeInfoKind.Object, Properties.Count: 0 };
			}
			catch (Exception)
			{
				// A type that System.Text.Json does not support: writing the value fails, and Write says why.
				return false;
			}
		}
	}

	private sealed class TextConverter<T> : JsonConverter<T>
	{
		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value is null or DBNull or INullable { IsNull: true })
			{
				writer.WriteNullValue();
				return;
			}

			Type type = value.GetType();
			if (!OverridesToString(type))
			{
				throw new InvalidOperationException(
					$"The type {type} has no public properties and does not override ToString(), so its values cannot be told apart."
				);
			}

			writer.WriteStringValue(value is IFormattable formattable
				? formattable.ToString(null, CultureInfo.InvariantCulture)
				: value.ToString());
		}
	}
}
