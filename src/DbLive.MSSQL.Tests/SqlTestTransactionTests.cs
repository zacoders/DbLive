using DbLive.Adapter;
using DbLive.Deployers.Testing;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// A ROLLBACK without a savepoint name ends the transaction of the test, also inside a procedure: the usual CATCH block
/// with <c>if @@trancount &gt; 0 rollback</c> does it. Without IMPLICIT_TRANSACTIONS the next statements would run in autocommit mode and
/// be committed at once, and Transaction.Current stays Active. The test fails, and only what a COMMIT committed stays in the database.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestTransactionTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output)
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

	[Theory]
	// the code under test rolls back the transaction, and the test goes on: its assertion passes on data it did not prepare.
	[InlineData("Tests/code.sql", $$"""
		insert into dbo.T ( Id, Note ) values ( 100, 'arrange' );
		{{CatchRollback}}
		select count(*) as cnt from dbo.T where Id = 200;
		select assert = 'rows';
		select 1 as cnt;
		""", new string[0])]
	// setup.test.sql ends the transaction: the test does not run.
	[InlineData("Tests/Setup/plain.sql", "insert into dbo.T ( Id, Note ) values ( 300, 'test' );", new string[0])]
	// what the test committed stays.
	[InlineData("Tests/commit.sql", "insert into dbo.T ( Id, Note ) values ( 200, 'test' ); commit;", new[] { "200" })]
	public async Task Test_that_ends_its_transaction_fails_and_only_what_it_committed_stays(string testPath, string test, string[] rowsLeft)
	{
		using TempProject project = CreateProject()
			.AddFile(testPath, test)
			.AddFile("Tests/Setup/setup.test.sql", CatchRollback);

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, testPath);

		Assert.False(result.IsSuccess);
		Assert.Equal(UnitTestItemRunner.TransactionEndedMessage, result.ErrorMessage);

		Assert.Equal(rowsLeft, await builder.GetRequiredService<IDbLiveDA>().ListAsync("select Id from dbo.T"));
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

		TestRunResult result = await builder.CreateTester().RunTestAsync(Output.WriteLine, "Tests/add-order-error.sql");

		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Empty(await builder.GetRequiredService<IDbLiveDA>().ListAsync("select Id from dbo.T"));
	}

	private static TempProject CreateProject()
	{
		return new TempProject()
			.AddFile("Migrations/001.migration.sql", Table)
			.AddFile("Code/dbo/Fail.sql", RollbackProc)
			.AddFile("Code/dbo/AddOrder.sql", SavepointProc);
	}
}
