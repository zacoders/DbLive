using DbLive.Adapter;
using DbLive.Project;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// 0096-87, reproduced: a script that ends the transaction it runs in without an error (<c>if @@trancount &gt; 0 rollback</c>).
/// The statements before the rollback are undone, the ones after it run in autocommit mode and are committed at once, together with
/// the rows of DbLive written after them. These tests check what DbLive does now; the fix of 0096-87 changes their assertions.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ScriptEndsTransactionTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	// {0} is undone by the rollback, {1} is committed at once.
	private const string RollbackScript = """
		insert into dbo.T ( Id ) values ( {0} );
		if @@trancount > 0 rollback;
		insert into dbo.T ( Id ) values ( {1} );
		""";

	private const string AfterDeploy = "insert into dbo.T ( Id ) select 100 where not exists ( select 1 from dbo.T where Id = 100 );";

	// SQL Server: "The COMMIT TRANSACTION request has no corresponding BEGIN TRANSACTION."
	private const int CommitWithoutTransactionError = 3902;

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Migration_that_ends_its_transaction_is_recorded_as_applied_and_skipped_by_the_next_deployment()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project
			.AddFile("Migrations/002.migration.sql", string.Format(RollbackScript, 1, 2))
			.AddFile("Migrations/003.migration.sql", "insert into dbo.T ( Id ) values ( 3 );");

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));

		// the commit of the migration scope: the message does not say that the script ended the transaction.
		Assert.Equal(CommitWithoutTransactionError, Assert.IsType<SqlException>(Assert.IsType<TransactionAbortedException>(ex.InnerException).InnerException).Number);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(MigrationItemStatus.Applied, await GetStatusAsync(da, 2));
		Assert.Equal(["2"], await ListAsync(da, "select Id from dbo.T"));

		await DeployAsync(project);

		// 002 is not run again: the row 1 is never inserted.
		Assert.Equal(MigrationItemStatus.Applied, await GetStatusAsync(da, 3));
		Assert.Equal(["2", "3"], await ListAsync(da, "select Id from dbo.T"));
	}

	// a procedure that ends the transaction of its caller is not silent: SQL Server fails the script with Msg 266 when it returns.
	// What the script ran after the call stays committed.
	[Fact]
	public async Task Migration_whose_procedure_ends_its_transaction_fails_with_error_266()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project.AddFile("Code/Swallow.sql", """
				create or alter procedure dbo.Swallow
				as
				begin
					begin try
						begin transaction;
						throw 50000, 'boom', 1;
					end try
					begin catch
						if @@trancount > 0 rollback;
					end catch
				end
				""");

		// migrations run before code: the procedure is deployed first.
		await DeployAsync(project);
		_ = project.AddFile("Migrations/002.migration.sql", "insert into dbo.T ( Id ) values ( 1 ); exec dbo.Swallow; insert into dbo.T ( Id ) values ( 2 );");

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));
		Assert.Equal(266, Assert.IsType<SqlException>(Assert.IsType<DbLiveSqlException>(ex.InnerException).InnerException).Number);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(MigrationItemStatus.None, await GetStatusAsync(da, 2));
		Assert.Equal(["2"], await ListAsync(da, "select Id from dbo.T"));
	}

	[Fact]
	public async Task Migration_that_ends_the_deployment_transaction_loses_its_row_and_leaves_its_statements_after_the_rollback()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project.AddFile("Migrations/002.migration.sql", string.Format(RollbackScript, 1, 2));

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));

		// the rollback undid the rows that this deployment saved for its migrations, so the row 'applied' has nothing to update.
		_ = Assert.IsType<DbLiveMigrationItemMissedSqlException>(ex.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select version from dblive.migration where version = 2"));
		Assert.Equal(["2"], await ListAsync(da, "select Id from dbo.T"));

		// the next deployment runs the script again, on top of what it left.
		ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));
		Assert.StartsWith("Violation of PRIMARY KEY constraint", Assert.IsType<DbLiveSqlException>(ex.InnerException).Message);
	}

	[Fact]
	public async Task Code_file_that_ends_the_deployment_transaction_is_recorded_as_applied_without_its_batches_before_the_rollback()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project
			.AddFile("Code/a.sql", $"""
				create or alter view dbo.v1 as select 1 as x;
				go
				{string.Format(RollbackScript, 1, 2)}
				go
				create or alter view dbo.v2 as select 2 as x;
				""")
			.AddFile("Code/c.sql", "create or alter view dbo.v3 as select 3 as x;")
			.AddFile("AfterDeploy/after.sql", AfterDeploy);

		// not a DbLive exception, and no file in it.
		TransactionAbortedException ex = await Assert.ThrowsAsync<TransactionAbortedException>(() => DeployAsync(project));
		Assert.Equal(CommitWithoutTransactionError, Assert.IsType<SqlException>(ex.InnerException).Number);

		// everything after the rollback is committed: the rest of the file, the files after it, AfterDeploy, and their rows.
		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(["100", "2"], await ListAsync(da, "select Id from dbo.T"));
		Assert.Equal(["code/a.sql:applied", "code/c.sql:applied"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
		Assert.Equal(["v2", "v3"], await ListAsync(da, "select name from sys.views"));

		// the file is skipped by its hash: dbo.v1 is never created.
		await DeployAsync(project);
		Assert.Equal(["v2", "v3"], await ListAsync(da, "select name from sys.views"));
	}

	[Fact]
	public async Task BeforeDeploy_script_that_ends_the_deployment_transaction_fails_the_deployment_after_it_is_committed()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project
			.AddFile("BeforeDeploy/rollback.sql", string.Format(RollbackScript, 1, 2))
			.AddFile("Code/v.sql", "create or alter view dbo.v as select 1 as x;")
			.AddFile("AfterDeploy/after.sql", AfterDeploy);

		TransactionAbortedException ex = await Assert.ThrowsAsync<TransactionAbortedException>(() => DeployAsync(project));
		Assert.Equal(CommitWithoutTransactionError, Assert.IsType<SqlException>(ex.InnerException).Number);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(["100", "2"], await ListAsync(da, "select Id from dbo.T"));
		Assert.Equal(["code/v.sql:applied"], await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
		Assert.Equal(["afterdeploy/after.sql", "beforedeploy/rollback.sql"], await ListAsync(da, "select relative_path from dblive.folder_items"));
	}

	[Theory]
	// the file fails on its commit, and the error does not say that the file ended the transaction.
	[InlineData(false, "The COMMIT TRANSACTION request has no corresponding BEGIN TRANSACTION.")]
	// another file of the round succeeds, so the file is tried again in the next round, on top of what its first attempt left:
	// the error of the retry is the one reported.
	[InlineData(true, "Violation of PRIMARY KEY constraint")]
	public async Task Code_file_that_ends_its_own_transaction_leaves_its_batches_after_the_rollback(bool withAnotherFile, string error)
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project.AddFile("Code/a.sql", $"""
			create or alter view dbo.v1 as select 1 as x;
			go
			{string.Format(RollbackScript, 1, 2)}
			go
			create or alter view dbo.v2 as select 2 as x;
			""");
		if (withAnotherFile)
		{
			_ = project.AddFile("Code/c.sql", "create or alter view dbo.v3 as select 3 as x;");
		}

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => DeployAsync(project));

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.StartsWith(error, Assert.IsType<DbLiveSqlException>(codeEx.InnerException).Message);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(["2"], await ListAsync(da, "select Id from dbo.T"));
		Assert.Contains("v2", await ListAsync(da, "select name from sys.views"));
		Assert.DoesNotContain("v1", await ListAsync(da, "select name from sys.views"));
		Assert.Contains("code/a.sql:error", await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
	}

	/// <summary>
	/// The project with <c>dbo.T</c>, deployed: a script of the next deployment that rolls back the deployment transaction would undo
	/// the table too.
	/// </summary>
	private async Task<TempProject> DeployTableAsync(TransactionWrapLevel level)
	{
		TempProject project = new TempProject()
			.AddFile("settings.json", $$"""{ "TransactionWrapLevel": "{{level}}" }""")
			.AddFile("Migrations/001.migration.sql", "create table dbo.T ( Id int not null primary key );");

		await DeployAsync(project);

		return project;
	}

	/// <summary>
	/// A builder reads the project once: a new build of the folder takes a new builder, like a new run of a release pipeline.
	/// </summary>
	private Task DeployAsync(TempProject project) => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default);

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<MigrationItemStatus> GetStatusAsync(IDbLiveDA da, long version)
	{
		IReadOnlyCollection<MigrationItemDto> items = await da.GetMigrationsAsync();
		return items.Single(i => i.Version == version && i.ItemType == MigrationItemType.Migration).Status;
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.QuerySqlAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
