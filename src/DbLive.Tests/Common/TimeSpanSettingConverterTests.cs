using System.Globalization;
using System.Text.Json;

namespace DbLive.Tests.Common;

/// <summary>
/// The time spans of settings.json and of the item settings files. DbLive reads them with its own converter, not with the
/// one of the runtime, so these tests on net8.0 hold on net9.0 and net10.0 too.
/// </summary>
public class TimeSpanSettingConverterTests
{
	private const string ItemSettingsFile = "Migrations/004.migration.settings.json";

	private const string FormatError =
		"""must be a time span "hh:mm:ss" or "d.hh:mm:ss", such as "00:00:30" or "1.00:00:00".""";

	[Theory]
	[InlineData("00:00:30", "00:00:30")]
	[InlineData("01:30:00", "01:30:00")]
	[InlineData("1:00:00", "01:00:00")]
	[InlineData("1.00:00:00", "1.00:00:00")]
	[InlineData("60.00:00:00", "60.00:00:00")]
	[InlineData("00:00:00.5", "00:00:00.5")]
	// no limit.
	[InlineData("00:00:00", "00:00:00")]
	public void Time_span_is_read(string value, string expected)
	{
		TimeSpan expectedValue = TimeSpan.ParseExact(expected, "c", CultureInfo.InvariantCulture);

		DbLiveSettings settings = SettingsTools.GetSettings<DbLiveSettings>($$"""{ "CodeItemTimeout": "{{value}}" }""");
		MigrationItemSettings itemSettings = SettingsTools.GetMigrationItemSettings($$"""{ "Timeout": "{{value}}" }""", ItemSettingsFile);

		Assert.Equal(expectedValue, settings.CodeItemTimeout);
		Assert.Equal(expectedValue, itemSettings.Timeout);
	}

	[Theory]
	// the short forms of the .NET format c: .NET 9 and later read "60" as 60 days and "02:00" as 2 hours, .NET 8 rejects them.
	[InlineData("\"60\"")]
	[InlineData("\"02:00\"")]
	[InlineData("\"1.02:03\"")]
	// hours up to 23, minutes and seconds up to 59, at most 7 digits of a fraction.
	[InlineData("\"24:00:00\"")]
	[InlineData("\"00:60:00\"")]
	[InlineData("\"00:00:00.12345678\"")]
	[InlineData("\"\"")]
	[InlineData("\"30 seconds\"")]
	// a JSON number: seconds or days, nobody can tell.
	[InlineData("60")]
	[InlineData("true")]
	[InlineData("{}")]
	public void Time_span_in_another_form_is_an_error(string json)
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>($$"""{ "CodeItemTimeout": {{json}} }""")
		);

		Assert.Equal($"The value of 'CodeItemTimeout' {FormatError} Path: $.CodeItemTimeout.", e.Message);
		Assert.Equal("$.CodeItemTimeout", e.Path);
	}

	[Theory]
	[InlineData("-00:00:30")]
	[InlineData("-1.00:00:00")]
	public void Negative_time_span_is_an_error(string value)
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>($$"""{ "CodeItemTimeout": "{{value}}" }""")
		);

		Assert.Equal("The value of 'CodeItemTimeout' cannot be negative. Path: $.CodeItemTimeout.", e.Message);
	}

	[Fact]
	public void Null_time_span_of_the_project_is_an_error()
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>("""{ "MigrationTimeout": null }""")
		);

		Assert.Equal("The value of 'MigrationTimeout' cannot be null. Path: $.MigrationTimeout.", e.Message);
	}

	[Fact]
	public void Error_of_an_item_names_the_file_and_the_key()
	{
		InvalidMigrationItemSettingsException e = Assert.Throws<InvalidMigrationItemSettingsException>(
			() => SettingsTools.GetMigrationItemSettings("""{ "Timeout": "02:00" }""", ItemSettingsFile)
		);

		Assert.Equal($"Invalid migration settings file {ItemSettingsFile}: The value of 'Timeout' {FormatError} Path: $.Timeout.", e.Message);
		Assert.Equal(ItemSettingsFile, e.RelativePath);
		Assert.IsAssignableFrom<JsonException>(e.InnerException);
	}
}
