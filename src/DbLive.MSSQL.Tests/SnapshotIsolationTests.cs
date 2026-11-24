using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// <c>Snapshot</c> in <c>settings.json</c>. With <c>TransactionWrapLevel</c> <c>Deployment</c> the commands of DbLive run in the snapshot
/// transaction too, where SQL Server does not allow index DDL (Msg 3964), also on a temporary table.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SnapshotIsolationTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Theory]
	[InlineData("None")]
	[InlineData("Migration")]
	[InlineData("Deployment")]
	public async Task Project_with_snapshot_isolation_deploys(string transactionWrapLevel)
	{
		IDbLiveDA da = CreateBuilder().GetRequiredService<IDbLiveDA>();
		await da.ExecuteSqlAsync($"alter database [{DbName}] set allow_snapshot_isolation on;");

		using TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{transactionWrapLevel}}", "TransactionIsolationLevel": "Snapshot" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Code/unchanged.sql", "create or alter view dbo.v_unchanged as select 1 as one;")
			.AddFile("Code/changed.sql", "create or alter view dbo.v_changed as select 1 as one;")
			.AddFile("Code/removed.sql", "create or alter view dbo.v_removed as select 1 as one;");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		// the second deployment runs each command of DbLive with the paths of code files: an unchanged file is marked verified,
		// a changed one applying, a removed one removed.
		_ = project
			.DeleteFiles("removed.sql")
			.AddFile("Code/changed.sql", "create or alter view dbo.v_changed as select 2 as two;");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			["code/changed.sql:applied", "code/removed.sql:removed", "code/unchanged.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);
	}
}
