using DbLive.Adapter;
using DbLive.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;

/// <summary>
/// On SQL Server the code under test can end the transaction of the test (see the tests of the same name there). On PostgreSQL
/// a procedure called inside a transaction block cannot: ROLLBACK or COMMIT in it fails with 2D000, and the test fails with that error.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlTestTransactionTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	// DbLive deploys to an existing database without creating it.
	public Task InitializeAsync() => new DbLiveBuilder().PostgreSQL().SetDbConnection(_dbCnnString).GetRequiredService<IDbLiveDA>().CreateDBAsync();

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Procedure_that_rolls_back_fails_the_test_and_leaves_nothing()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t ( id int not null primary key, note text not null );")
			.AddFile("Code/fail.sql", """
				create or replace procedure fail()
				language plpgsql
				as $$
				begin
					insert into t ( id, note ) values ( 1, 'fail' );
					rollback;
				end $$;
				""")
			.AddFile("Tests/fail.sql", """
				insert into t ( id, note ) values ( 100, 'arrange' );
				call fail();
				insert into t ( id, note ) values ( 200, 'after' );
				""");

		DbLiveBuilder builder = CreateBuilder(project);
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		TestRunResult result = await builder.CreateTester().RunTestAsync(_output.WriteLine, "Tests/fail.sql");

		Assert.False(result.IsSuccess);
		Assert.Contains("invalid transaction termination", result.ErrorMessage);

		List<SqlResult> rows = await builder.GetRequiredService<IDbLiveDA>().ExecuteQueryMultipleAsync("select id from t");
		Assert.Empty(rows.Single().Rows);
	}

	private DbLiveBuilder CreateBuilder(TempProject project)
	{
		return new DbLiveBuilder()
			.ConfigureServices(s => s.AddXUnitLogger(_output))
			.PostgreSQL()
			.SetDbConnection(_dbCnnString)
			.SetScriptsFolder(project.Path);
	}
}
