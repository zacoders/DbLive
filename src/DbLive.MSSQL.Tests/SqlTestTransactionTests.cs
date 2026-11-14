using DbLive.Adapter;
using DbLive.Deployers.Testing;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A ROLLBACK without a savepoint name ends the transaction of the test, also inside a procedure: the usual CATCH block
/// with <c>if @@trancount &gt; 0 rollback</c> does it. The next statements run in autocommit mode and are committed at once,
/// and Transaction.Current stays Active. The test fails, and what ran after the end of the transaction stays in the database.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestTransactionTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>, IAsyncLifetime
{
	private const string Table = "create table dbo.T ( Id int not null primary key, Note nvarchar(4000) not null );";

	// the usual error handler: it rolls back the transaction of the caller too.
	private const string RollbackProc = """
		create or alter proc dbo.Fail
		as
		begin
			begin try
				begin transaction;
				insert into dbo.T ( Id, Note ) values ( 1, 'fail' );
				throw 50000, 'boom', 1;
				commit;
			end try
			begin catch
				if @@trancount > 0 rollback;
				throw;
			end catch
		end
		""";

	// inside a transaction of its caller it rolls back to its own savepoint only.
	private const string SavepointProc = """
		create or alter proc dbo.AddOrder
			@Id int
		as
		begin
			declare @trancount int = @@trancount;
			begin try
				if @trancount = 0 begin transaction;
				else save transaction AddOrder;

				insert into dbo.T ( Id, Note ) values ( @Id, 'order' );
				if @Id < 0 throw 50000, 'Id must be positive.', 1;

				if @trancount = 0 commit;
			end try
			begin catch
				if xact_state() = -1 or ( xact_state() = 1 and @trancount = 0 ) rollback;
				else if xact_state() = 1 rollback transaction AddOrder;
				throw;
			end catch
		end
		""";

	private const string CatchRollback = """
		begin try
			exec dbo.Fail;
		end try
		begin catch
			insert into dbo.T ( Id, Note ) values ( 200, error_message() );
		end catch
		""";

	private readonly string _dbCnnString = _fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().SqlServer().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Theory]
	// the code under test rolls back the transaction, and the test goes on: its assertion passes on data it did not prepare.
	[InlineData("Tests/code.sql", $$"""
		insert into dbo.T ( Id, Note ) values ( 100, 'arrange' );
		{{CatchRollback}}
		select count(*) as cnt from dbo.T where Id = 200;
		select assert = 'rows';
		select 1 as cnt;
		""")]
	// setup.test.sql ends the transaction: the test after it runs in autocommit mode too.
	[InlineData("Tests/Setup/plain.sql", "insert into dbo.T ( Id, Note ) values ( 300, 'test' );")]
	[InlineData("Tests/commit.sql", "insert into dbo.T ( Id, Note ) values ( 200, 'test' ); commit;")]
	public async Task Test_that_ends_its_transaction_fails_and_what_ran_after_it_stays(string testPath, string test)
	{
		using TempProject project = CreateProject()
			.AddFile(testPath, test)
			.AddFile("Tests/Setup/setup.test.sql", CatchRollback);

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, testPath);

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.ErrorMessage);

		// committed: the rollback at the end of the test has nothing to undo.
		Assert.NotEmpty(await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select Id from dbo.T"));
	}

	[Fact]
	public async Task Error_path_of_a_procedure_that_rolls_back_to_its_savepoint_passes_and_leaves_nothing()
	{
		using TempProject project = CreateProject()
			.AddFile("Tests/add-order-error.sql", """
				insert into dbo.T ( Id, Note ) values ( 100, 'arrange' );

				begin try
					exec dbo.AddOrder @Id = -1;
					throw 50001, 'Expected an error.', 1;
				end try
				begin catch
					if error_number() = 50001 throw;
					select error_message() as Error;
				end catch

				select Id, Note from dbo.T order by Id;

				select assert = 'rows';
				select 'Id must be positive.' as Error;
				select 100 as Id, 'arrange' as Note;
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/add-order-error.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Empty(await ListAsync(builder.GetRequiredService<IDbLiveDA>(), "select Id from dbo.T"));
	}

	private static TempProject CreateProject()
	{
		return new TempProject()
			.AddFile("Migrations/001.migration.sql", Table)
			.AddFile("Code/dbo/Fail.sql", RollbackProc)
			.AddFile("Code/dbo/AddOrder.sql", SavepointProc);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}

	private static async Task<List<string>> ListAsync(IDbLiveDA da, string query)
	{
		List<SqlResult> results = await da.ExecuteQueryMultipleAsync(query);
		return [.. results.Single().Rows.Select(r => Convert.ToString(r[0])!).Order(StringComparer.Ordinal)];
	}
}
