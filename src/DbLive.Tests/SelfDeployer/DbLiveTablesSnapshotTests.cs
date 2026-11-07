using DbLive.MSSQL;
using DbLive.PostgreSQL;
using System.Runtime.CompilerServices;

namespace DbLive.Tests.SelfDeployer;

/// <summary>
/// What an older DbLive depends on in the <c>dblive</c> tables: the self-migrations of both providers and the values
/// of the enums stored in the tables. An older DbLive meets a database this one has deployed when an older build is deployed
/// again, so a change here is a decision: does an older DbLive still read and write the tables correctly?
/// It does not see a change of behavior in C# alone (a path format, a new meaning of a value):
/// "Compatibility of the dblive tables" in Development.md covers that.
/// </summary>
public class DbLiveTablesSnapshotTests
{
	// update after the decision the failure message asks for.
	private static readonly string[] _snapshot = [
		"MinCompatibleVersion 2.0.0",
		"self-migration mssql 01.migrations.sql 4726503564446519789",
		"self-migration mssql 02.code.sql -2717960315347916332",
		"self-migration mssql 03.unit-tests.sql -4198994666110651958",
		"self-migration mssql 04.folder-items.sql -5546103530288288273",
		"self-migration mssql 05.migration-version-bigint.sql 9063260521949538315",
		"self-migration mssql 06.project-id.sql 7741027976536260751",
		"self-migration mssql 07.item-settings.sql -2757651216570654091",
		"self-migration mssql 08.relative-path-separator.sql 5200764250627927877",
		"self-migration mssql 09.relative-path-lower-case.sql 3412579106601931037",
		"self-migration mssql 10.code-item-type.sql 3550310615360531122",
		"self-migration mssql 11.remove-undo.sql -2866849754408976792",
		"self-migration mssql 12.min-dblive-lib-version.sql 2255103403618828717",
		"self-migration postgresql 01.migration.sql -8163480040521449432",
		"self-migration postgresql 02.migration-version-bigint.sql 6347519236500195150",
		"self-migration postgresql 03.project-id.sql -6108490539713440686",
		"self-migration postgresql 04.item-settings.sql 5938028415325629468",
		"self-migration postgresql 05.relative-path-separator.sql -7656279710988285539",
		"self-migration postgresql 06.relative-path-lower-case.sql -2319486233948798156",
		"self-migration postgresql 07.code-item-type.sql 8923038393293824789",
		"self-migration postgresql 08.remove-undo.sql -3806047876693741311",
		"self-migration postgresql 09.min-dblive-lib-version.sql 1864736282002446523",
		"dblive.migration.item_type migration",
		"dblive.migration.item_type breaking",
		"dblive.migration.item_type migration.settings",
		"dblive.migration.item_type breaking.settings",
		"dblive.migration.status none",
		"dblive.migration.status skipped",
		"dblive.migration.status applied",
		"dblive.code.item_type code",
		"dblive.code.item_type breaking",
		"dblive.code.status error",
		"dblive.code.status skipped",
		"dblive.code.status applied",
		"dblive.code.status removed",
		"dblive.folder_items.folder_type BeforeDeploy",
		"dblive.folder_items.folder_type AfterDeploy",
	];

	[Fact]
	public async Task The_dblive_tables_are_the_ones_of_the_snapshot()
	{
		List<string> actual = await DescribeAsync();

		if (actual.SequenceEqual(_snapshot)) return;

		string changes = string.Join(Environment.NewLine, [
			.. actual.Except(_snapshot).Select(line => $"  + {line}"),
			.. _snapshot.Except(actual).Select(line => $"  - {line}")
		]);

		Assert.Fail($"""
			The dblive tables changed:
			{changes}

			An older DbLive meets a database this DbLive has deployed when an older build is deployed again. Decide whether it still works
			("Compatibility of the dblive tables" in Development.md). If it misreads or miswrites the dblive tables after this change, raise
			DbLiveCompatibility.MinCompatibleVersion to the version of the release that ships the change, and say in the release note
			that older DbLive versions can no longer deploy the database. In both cases update the snapshot in {SourceFile()}:

			{string.Join(Environment.NewLine, actual.Select(line => $"\t\t\"{line}\","))}
			""");
	}

	private static async Task<List<string>> DescribeAsync()
	{
		List<string> lines = [$"MinCompatibleVersion {DbLiveCompatibility.MinCompatibleVersion}"];

		foreach ((string provider, Type daType) in new[] { ("mssql", typeof(MsSqlDA)), ("postgresql", typeof(PostgreSqlDA)) })
		{
			InternalDbLiveProject project = new(new InternalScriptSource(daType.Assembly));

			lines.AddRange((await project.GetMigrationsAsync())
				.Select(m => $"self-migration {provider} {m.FileData.RelativePath} {m.FileData.ContentHash}"));
		}

		// the values as the providers write them.
		lines.AddRange(Enum.GetValues<MigrationItemType>().Select(v => $"dblive.migration.item_type {MigrationItemTypeMapper.ToDbValue(v)}"));
		lines.AddRange(Enum.GetValues<MigrationItemStatus>().Select(v => $"dblive.migration.status {v.ToString().ToLower()}"));
		lines.AddRange(Enum.GetValues<CodeItemType>().Select(v => $"dblive.code.item_type {v.ToString().ToLower()}"));
		lines.AddRange(Enum.GetValues<CodeItemStatus>().Select(v => $"dblive.code.status {v.ToString().ToLower()}"));
		lines.AddRange(Enum.GetValues<ProjectFolder>().Select(v => $"dblive.folder_items.folder_type {v}"));

		return lines;
	}

	private static string SourceFile([CallerFilePath] string path = "") => path;
}
