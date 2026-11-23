using DbLive.Adapter;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Outside the deployment transaction a code file runs in a transaction of its own: a file whose later <c>GO</c> batch fails leaves
/// none of its batches applied. A file with In-Memory OLTP DDL runs without one: SQL Server does not allow that DDL in a transaction.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class CodeFileTransactionTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
{
	[Theory]
	[InlineData("")]
	// the error rolls back the transaction itself, and there is nothing left to roll back.
	[InlineData("set xact_abort on;\ngo\n")]
	public async Task Code_file_whose_later_batch_fails_leaves_none_of_its_batches(string prefix)
	{
		using TempProject project = new TempProject()
			.AddFile("Code/a.sql", prefix + "create or alter view dbo.v1 as select 1 as x;\ngo\ncreate or alter view dbo.v2 as select x from dbo.missing;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Invalid object name 'dbo.missing'.", Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Empty(await da.ListAsync("select name from sys.views"));
		// a failure writes no row.
		Assert.Empty(await da.ListAsync("select relative_path from dblive.code"));

		// the connection of the file went back to the pool without an open transaction, which would keep its locks.
		Assert.Equal(["0"], await da.ListAsync("select count(*) from sys.dm_exec_sessions where database_id = db_id() and open_transaction_count > 0 and session_id <> @@spid"));
	}

	[Fact]
	public async Task Code_file_runs_at_the_configured_isolation_level_in_its_transaction()
	{
		using TempProject project = new TempProject()
			.AddFile("settings.json", """{ "TransactionIsolationLevel": "Serializable" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.T ( level int, user_transactions int );")
			// not @@trancount: an insert without a transaction counts its own.
			.AddFile("Code/a.sql", """
				insert into dbo.T
				select
					transaction_isolation_level,
					(select count(*) from sys.dm_tran_session_transactions where session_id = @@spid and is_user_transaction = 1)
				from sys.dm_exec_sessions
				where session_id = @@spid;
				""");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// 4: serializable; 1: in a transaction that began before the file.
		Assert.Equal(["4:1"], await builder.GetRequiredService<IDbLiveDA>().ListAsync("select concat(level, ':', user_transactions) from dbo.T"));
	}

	[Fact]
	public async Task Statement_not_allowed_in_a_transaction_fails_in_a_code_file()
	{
		using TempProject project = new TempProject()
			.AddFile("Code/a.sql", "alter database current set recovery simple;");

		DbLiveBuilder builder = CreateBuilder(project);

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(
			() => builder.CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal(
			"ALTER DATABASE statement not allowed within multi-statement transaction.",
			Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message
		);
	}

	// SQL Server does not allow In-Memory OLTP DDL in a user transaction (Msg 12331, which rolls the transaction back): such a file runs
	// without a transaction of its own. A new procedure, then a change of the one that exists.
	[Fact]
	public async Task Code_file_with_in_memory_oltp_ddl_deploys_without_a_transaction()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", MemoryOptimizedFilegroup)
			// ALTER DATABASE is not allowed in a transaction either.
			.AddFile("Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None" }""")
			.AddFile("Code/types/Ids.sql", IdsType)
			.AddFile("Code/CountIds.sql", CountIdsProcedure("count(*)"));

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		_ = project.AddFile("Code/CountIds.sql", CountIdsProcedure("count(*) * 10"));

		// a builder reads the project once.
		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(
			["code/countids.sql:applied", "code/types/ids.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);
		Assert.Equal(["20"], await da.ListAsync("declare @ids dbo.Ids; insert into @ids values (1), (2); exec dbo.CountIds @ids;"));
	}

	// the batches of such a file commit one by one: the drop commits, the create fails, and the procedure is missing. The row of the file
	// is applying, so the content of its last success runs when it comes back (git revert, the previous release) and restores it.
	[Fact]
	public async Task In_memory_oltp_file_that_failed_in_part_is_restored_by_the_content_of_its_last_success()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", MemoryOptimizedFilegroup)
			.AddFile("Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None" }""")
			.AddFile("Code/types/Ids.sql", IdsType)
			.AddFile("Code/CountIds.sql", CountIdsProcedure("count(*)"));

		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		_ = project.AddFile("Code/CountIds.sql", "drop procedure if exists dbo.CountIds;\ngo\n" + CountIdsProcedure("count(no_such_column)"));

		DbLiveBuilder builder = CreateBuilder(project);
		_ = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => builder.CreateDeployer().DeployAsync(DeployParameters.Default));

		IDbLiveDA da = builder.GetRequiredService<IDbLiveDA>();
		Assert.Equal(0, await da.ScalarAsync("select count(*) from sys.procedures where object_id = object_id('dbo.CountIds')"));
		Assert.Equal(
			["code/countids.sql:applying", "code/types/ids.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);

		_ = project.AddFile("Code/CountIds.sql", CountIdsProcedure("count(*)"));
		await CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

		Assert.Equal(["2"], await da.ListAsync("declare @ids dbo.Ids; insert into @ids values (1), (2); exec dbo.CountIds @ids;"));
		Assert.Equal(
			["code/countids.sql:applied", "code/types/ids.sql:applied"],
			await da.ListAsync("select concat(relative_path, ':', status) from dblive.code")
		);
	}

	private const string IdsType = """
		if type_id('dbo.Ids') is null
			create type dbo.Ids as table ( id int not null primary key nonclustered ) with ( memory_optimized = on );
		""";

	// In-Memory OLTP needs a filegroup for its data, also for natively compiled modules and memory-optimized table types.
	private const string MemoryOptimizedFilegroup = """
		declare @db sysname = db_name();
		declare @addFilegroup nvarchar(max) = N'alter database ' + quotename(@db) + N' add filegroup imoltp contains memory_optimized_data;';
		declare @addFile nvarchar(max) = N'alter database ' + quotename(@db) + N' add file ( name = imoltp, filename = '''
			+ cast(serverproperty('InstanceDefaultDataPath') as nvarchar(max)) + @db + N'_imoltp'' ) to filegroup imoltp;';
		exec sys.sp_executesql @addFilegroup;
		exec sys.sp_executesql @addFile;
		""";

	private static string CountIdsProcedure(string count) => $"""
		create or alter procedure dbo.CountIds
			@ids dbo.Ids readonly
		with native_compilation, schemabinding
		as
		begin atomic with ( transaction isolation level = snapshot, language = N'us_english' )
			select {count} as n from @ids;
		end
		""";
}
