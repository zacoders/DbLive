using System.Text.Json;

namespace DbLive.Tests.Common;

/// <summary>
/// The enum keys of settings.json and of the item settings files: only the name of a value, in any case. DbLive reads them
/// with its own converter, not with JsonStringEnumConverter, so these tests on net8.0 hold on net9.0 and net10.0 too.
/// </summary>
public class EnumSettingConverterTests
{
	private const string ItemSettingsFile = "Migrations/004.migration.settings.json";

	private const string WrapLevelError = """must be "Deployment", "Migration" or "None".""";

	private const string IsolationLevelError = """must be "ReadCommitted", "RepeatableRead", "Serializable" or "Snapshot".""";

	[Fact]
	public void Name_of_a_value_is_read_in_any_case()
	{
		DbLiveSettings settings = SettingsTools.GetSettings<DbLiveSettings>("""
			{
				"TransactionWrapLevel": "none",
				"TransactionIsolationLevel": "SNAPSHOT",
				"TestsTransactionIsolationLevel": "RepeatableRead",
				"MigrationChecksumMode": "wARN"
			}
			""");
		MigrationItemSettings itemSettings = SettingsTools.GetMigrationItemSettings(
			"""{ "TransactionWrapLevel": "None", "TransactionIsolationLevel": "serializable" }""",
			ItemSettingsFile
		);

		Assert.Equal(TransactionWrapLevel.None, settings.TransactionWrapLevel);
		Assert.Equal(TranIsolationLevel.Snapshot, settings.TransactionIsolationLevel);
		Assert.Equal(TranIsolationLevel.RepeatableRead, settings.TestsTransactionIsolationLevel);
		Assert.Equal(MigrationChecksumMode.Warn, settings.MigrationChecksumMode);
		Assert.Equal(TransactionWrapLevel.None, itemSettings.TransactionWrapLevel);
		Assert.Equal(TranIsolationLevel.Serializable, itemSettings.TransactionIsolationLevel);
	}

	[Theory]
	// a number that is not a value: JsonStringEnumConverter took it, and every migration ran without a transaction.
	[InlineData("3")]
	[InlineData("-1")]
	// a number in a string: JsonStringEnumConverter took it, on .NET 8 also with allowIntegerValues: false.
	[InlineData("\"3\"")]
	[InlineData("\"-1\"")]
	// a number of a value: the enums are internal, their numbers are not documented.
	[InlineData("0")]
	[InlineData("\"1\"")]
	// names joined by a comma: JsonStringEnumConverter combined them, also for an enum without [Flags] ("Migration, None" is 3).
	[InlineData("\"Migration, None\"")]
	[InlineData("\"Deployment, Migration\"")]
	[InlineData("\" Migration\"")]
	[InlineData("\"Migrations\"")]
	[InlineData("\"\"")]
	[InlineData("true")]
	[InlineData("{}")]
	[InlineData("[]")]
	public void Value_that_is_not_a_name_is_an_error(string json)
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>($$"""{ "TransactionWrapLevel": {{json}} }""")
		);

		Assert.Equal($"The value of 'TransactionWrapLevel' {WrapLevelError} Path: $.TransactionWrapLevel.", e.Message);
		Assert.Equal("$.TransactionWrapLevel", e.Path);
	}

	[Theory]
	[InlineData("TransactionIsolationLevel", IsolationLevelError)]
	[InlineData("TestsTransactionIsolationLevel", IsolationLevelError)]
	[InlineData("MigrationChecksumMode", """must be "Strict" or "Warn".""")]
	public void Error_lists_the_values_of_the_key(string key, string expectedError)
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>($$"""{ "{{key}}": 7 }""")
		);

		Assert.Equal($"The value of '{key}' {expectedError} Path: $.{key}.", e.Message);
	}

	[Fact]
	public void Null_enum_of_the_project_is_an_error()
	{
		JsonException e = Assert.ThrowsAny<JsonException>(
			() => SettingsTools.GetSettings<DbLiveSettings>("""{ "TransactionWrapLevel": null }""")
		);

		Assert.Equal("The value of 'TransactionWrapLevel' cannot be null. Path: $.TransactionWrapLevel.", e.Message);
	}

	[Theory]
	[InlineData("TransactionIsolationLevel", "7", IsolationLevelError)]
	[InlineData("TransactionIsolationLevel", "\"7\"", IsolationLevelError)]
	[InlineData("TransactionWrapLevel", "3", WrapLevelError)]
	[InlineData("TransactionWrapLevel", "\"3\"", WrapLevelError)]
	public void Error_of_an_item_names_the_file_and_the_key(string key, string json, string expectedError)
	{
		InvalidMigrationItemSettingsException e = Assert.Throws<InvalidMigrationItemSettingsException>(
			() => SettingsTools.GetMigrationItemSettings($$"""{ "{{key}}": {{json}} }""", ItemSettingsFile)
		);

		Assert.Equal($"Invalid migration settings file {ItemSettingsFile}: The value of '{key}' {expectedError} Path: $.{key}.", e.Message);
		Assert.Equal(ItemSettingsFile, e.RelativePath);
		Assert.IsAssignableFrom<JsonException>(e.InnerException);
	}
}
