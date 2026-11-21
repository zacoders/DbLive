using DbLive.Adapter;
using DbLive.Project;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// 0096-87: a script that ends the transaction of DbLive without an error (<c>COMMIT</c>, <c>ROLLBACK</c>). DbLive compares the ID of
/// the transaction before and after the script and fails it. With <c>IMPLICIT_TRANSACTIONS ON</c> the statements of the script after
/// the end open a new transaction instead of committing at once, and DbLive rolls it back: what a <c>COMMIT</c> of the script committed
/// stays, nothing else.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class ScriptEndsTransactionTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	// {0} is undone by the rollback; {1} would be committed at once without IMPLICIT_TRANSACTIONS.
	private const string RollbackScript = """
		insert into dbo.T ( Id ) values ( {0} );
		if @@trancount > 0 rollback;
		insert into dbo.T ( Id ) values ( {1} );
		""";

	private const string AfterDeploy = "insert into dbo.T ( Id ) select 100 where not exists ( select 1 from dbo.T where Id = 100 );";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Theory]
	[InlineData("if @@trancount > 0 rollback", new string[0])]
	// what the script committed stays; the statements after the COMMIT are rolled back.
	[InlineData("commit", new[] { "1" })]
	// a transaction of the script's own after the end: @@trancount is as it was, the ID of the transaction is not.
	[InlineData("rollback; begin tran", new string[0])]
	[InlineData("commit; begin tran", new[] { "1" })]
	public async Task Migration_that_ends_its_transaction_fails_and_is_not_recorded(string end, string[] rowsLeft)
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project
			.AddFile("Migrations/002.migration.sql", $"insert into dbo.T ( Id ) values ( 1 ); {end}; insert into dbo.T ( Id ) values ( 2 );")
			.AddFile("Migrations/003.migration.sql", "insert into dbo.T ( Id ) values ( 3 );");

		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));

		DbLiveTransactionEndedException inner = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);
		Assert.StartsWith("Error deploying Migrations/002.migration.sql file.", ex.Message);
		Assert.Contains(inner.Message, ex.Message);
		Assert.StartsWith(
			"The script ended the transaction that DbLive runs it in: it ran COMMIT or ROLLBACK, for example 'if @@trancount > 0 rollback'",
			inner.Message
		);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(MigrationItemStatus.None, await GetStatusAsync(da, 2));
		Assert.Equal(MigrationItemStatus.None, await GetStatusAsync(da, 3));
		Assert.Equal(rowsLeft, await ListAsync(da, "select Id from dbo.T"));
	}

	// a procedure that ends the transaction of its caller: without IMPLICIT_TRANSACTIONS SQL Server fails the script with Msg 266 when it
	// returns, with it on it does not, and the check of the transaction ID fails the script. What the script ran after the call is rolled
	// back with the transaction that IMPLICIT_TRANSACTIONS opened for it.
	[Fact]
	public async Task Migration_whose_procedure_ends_its_transaction_fails_and_leaves_nothing()
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
		_ = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Equal(MigrationItemStatus.None, await GetStatusAsync(da, 2));
		Assert.Empty(await ListAsync(da, "select Id from dbo.T"));
	}

	[Fact]
	public async Task Migration_that_ends_the_deployment_transaction_fails_and_leaves_nothing()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project.AddFile("Migrations/002.migration.sql", string.Format(RollbackScript, 1, 2));

		// the next deployment fails the same way: nothing of the script stayed.
		for (int run = 1; run <= 2; run++)
		{
			MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(() => DeployAsync(project));
			_ = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);

			IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
			Assert.Empty(await ListAsync(da, "select version from dblive.migration where version = 2"));
			Assert.Empty(await ListAsync(da, "select Id from dbo.T"));
		}
	}

	[Fact]
	public async Task Code_file_that_ends_the_deployment_transaction_fails_at_once_and_leaves_nothing()
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

		CodeDeploymentAggregateException ex = await Assert.ThrowsAsync<CodeDeploymentAggregateException>(() => DeployAsync(project));

		CodeDeploymentException codeEx = Assert.IsType<CodeDeploymentException>(Assert.Single(ex.InnerExceptions));
		Assert.Equal("Deploy code file error. File path: Code/a.sql. The file ended the deployment transaction, so it is not retried.", codeEx.Message);
		_ = Assert.IsType<DbLiveTransactionEndedException>(codeEx.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select Id from dbo.T"));
		Assert.Empty(await ListAsync(da, "select relative_path from dblive.code"));
		Assert.Empty(await ListAsync(da, "select name from sys.views"));
	}

	[Fact]
	public async Task BeforeDeploy_script_that_ends_the_deployment_transaction_fails_and_leaves_nothing()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Deployment);
		_ = project
			.AddFile("BeforeDeploy/rollback.sql", string.Format(RollbackScript, 1, 2))
			.AddFile("Code/v.sql", "create or alter view dbo.v as select 1 as x;")
			.AddFile("AfterDeploy/after.sql", AfterDeploy);

		FolderDeploymentException ex = await Assert.ThrowsAsync<FolderDeploymentException>(() => DeployAsync(project));
		_ = Assert.IsType<DbLiveTransactionEndedException>(ex.InnerException);
		Assert.Contains("BeforeDeploy/rollback.sql", ex.Message);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select Id from dbo.T"));
		Assert.Empty(await ListAsync(da, "select relative_path from dblive.code"));
		Assert.Empty(await ListAsync(da, "select relative_path from dblive.folder_items"));
	}

	[Theory]
	// not retried when another file of the round succeeds: the error of the file is reported, not the error of a retry.
	[InlineData(false)]
	[InlineData(true)]
	public async Task Code_file_that_ends_its_own_transaction_fails_at_once_and_leaves_nothing(bool withAnotherFile)
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
		Assert.Equal("Deploy code file error. File path: Code/a.sql. The file ended its transaction, so it is not retried.", codeEx.Message);
		_ = Assert.IsType<DbLiveTransactionEndedException>(codeEx.InnerException);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		Assert.Empty(await ListAsync(da, "select Id from dbo.T"));
		Assert.DoesNotContain("v1", await ListAsync(da, "select name from sys.views"));
		Assert.DoesNotContain("v2", await ListAsync(da, "select name from sys.views"));
		Assert.Contains("code/a.sql:error", await ListAsync(da, "select concat(relative_path, ':', status) from dblive.code"));
	}

	// IMPLICIT_TRANSACTIONS is on only while a script runs in a transaction of DbLive: not for a script without a transaction, and not for
	// the commands after the script (the row of the migration is written on the same connection).
	[Fact]
	public async Task Implicit_transactions_are_on_only_for_a_script_in_a_transaction()
	{
		using TempProject project = await DeployTableAsync(TransactionWrapLevel.Migration);
		_ = project
			.AddFile("Migrations/002.migration.sql", "insert into dbo.T ( Id ) select @@options & 2;")
			.AddFile("Migrations/003.migration.sql", "insert into dbo.T ( Id ) select 10 + ( @@options & 2 );")
			.AddFile("Migrations/003.migration.settings.json", """{ "TransactionWrapLevel": "None" }""");

		await DeployAsync(project);

		IDbLiveDA da = CreateBuilder(project).GetRequiredService<IDbLiveDA>();
		// 2: on in the script in a transaction; 10: off in the script without one.
		Assert.Equal(["10", "2"], await ListAsync(da, "select Id from dbo.T"));
		Assert.Equal(MigrationItemStatus.Applied, await GetStatusAsync(da, 3));
		Assert.Equal(["0"], await ListAsync(da, "select @@options & 2"));
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
