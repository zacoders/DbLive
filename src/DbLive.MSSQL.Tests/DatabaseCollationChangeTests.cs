using DbLive.Adapter;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// <c>alter database ... collate</c> changes the collation of the database, and the columns of the <c>dblive</c> tables keep theirs.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DatabaseCollationChangeTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	private const string OldCollation = "Latin1_General_100_CI_AS";
	private const string NewCollation = "SQL_Latin1_General_CP1_CI_AS";

	[Fact]
	public async Task Code_deploys_after_the_collation_of_the_database_changed()
	{
		await ExecuteInMasterAsync($"create database [{DbName}] collate {OldCollation};");

		using TempProject project = new TempProject()
			.AddFile("Code/unchanged.sql", "create or alter view dbo.unchanged as select 1 as one;")
			.AddFile("Code/changed.sql", "create or alter view dbo.changed as select 1 as one;")
			.AddFile("Code/removed.sql", "create or alter view dbo.removed as select 1 as one;");

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		await ChangeCollationAsync();

		// the three commands that compare the paths with dblive.code: a file skipped by its hash, a file run again, a removed file.
		_ = project
			.AddFile("Code/changed.sql", "create or alter view dbo.changed as select 2 as two;")
			.DeleteFiles("removed.sql");

		DbLiveBuilder builder = CreateBuilder(project);
		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();

		Assert.Equal(
			[$"{NewCollation} {OldCollation}"],
			await da.ListAsync("""
				select concat(convert(sysname, databasepropertyex(db_name(), 'Collation')), ' ', collation_name)
				from sys.columns
				where object_id = object_id('dblive.code') and name = 'relative_path'
				""")
		);

		await da.ExecuteSqlAsync("update dblive.code set verified_utc = null;");

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(
			["code/changed.sql:applied", "code/removed.sql:removed", "code/unchanged.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);
		Assert.Equal(
			["code/removed.sql", "code/unchanged.sql"],
			await da.ListAsync("select relative_path from dblive.code where verified_utc is not null and relative_path <> 'code/changed.sql'")
		);
	}

	/// <summary>
	/// <c>dblive.version.one_row_lock</c> is a computed column, which blocks <c>alter database ... collate</c> (Msg 5075): the
	/// operator drops it, changes the collation and creates it again, as the message of SQL Server says. The sessions of the
	/// database end; their pooled connections are cleared.
	/// </summary>
	private async Task ChangeCollationAsync()
	{
		await ExecuteInMasterAsync($"""
			alter database [{DbName}] set single_user with rollback immediate;
			use [{DbName}];
			alter table dblive.version drop constraint pk_dblive_version;
			alter table dblive.version drop column one_row_lock;
			alter database [{DbName}] collate {NewCollation};
			alter table dblive.version add one_row_lock as 1;
			alter table dblive.version add constraint pk_dblive_version primary key ( one_row_lock );
			use master;
			alter database [{DbName}] set multi_user;
			""");

		SqlConnection.ClearPool(new SqlConnection(DbCnnString));
		SqlConnection.ClearPool(new SqlConnection(new SqlConnectionStringBuilder(DbCnnString) { ConnectRetryCount = 0 }.ConnectionString));
	}

	private async Task ExecuteInMasterAsync(string sql)
	{
		await using SqlConnection cnn = new(Fixture.MasterDbConnectionString);
		await cnn.OpenAsync();
		await using SqlCommand cmd = new(sql, cnn);
		_ = await cmd.ExecuteNonQueryAsync();
	}
}
