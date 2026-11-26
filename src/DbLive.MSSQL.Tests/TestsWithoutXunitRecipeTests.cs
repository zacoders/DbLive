using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// The recipe for the tests without xUnit on a server of your own ("Running all tests without xUnit" in
/// docs/unit-testing/index.md, Section 8.2 of docs/llms-full.txt): a database with a fixed name, dropped with the SQL of the
/// docs before the deployment. Two runs in one process: the connections of the first one stay in the pool.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class TestsWithoutXunitRecipeTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	// the SQL of the docs, as it is there.
	private const string DropTestDatabase = """
		if db_id('MyDb_Tests') is not null
		begin
			alter database [MyDb_Tests] set single_user with rollback immediate;
			drop database [MyDb_Tests];
		end
		""";

	private readonly string _connectionString = _fixture.MasterDbConnectionString.SetMsSqlDatabaseName("MyDb_Tests");

	[Fact]
	public async Task The_next_run_drops_the_database_while_the_connections_of_the_previous_run_use_it()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.Person (Id int not null);")
			.AddFile("Tests/no-person.sql", "select Id from dbo.Person; select 'empty' as assert;");

		await RunRecipeAsync(project);

		// data that the next run must not see, written on a connection that stays in the pool as well.
		await using (SqlConnection cnn = new(_connectionString))
		{
			await cnn.OpenAsync();
			await using SqlCommand insert = new("insert dbo.Person (Id) values (1);", cnn);
			_ = await insert.ExecuteNonQueryAsync();
		}
		Assert.True(await CountAsync("select count(*) from sys.dm_exec_sessions where database_id = db_id('MyDb_Tests')") > 0);

		await RunRecipeAsync(project);

		await DropAsync();
		Assert.Equal(0, await CountAsync("select count(*) from sys.databases where name = 'MyDb_Tests'"));
	}

	// the recipe of the docs, with the scripts of a folder instead of a SQL project.
	private async Task RunRecipeAsync(TempProject project)
	{
		await DropAsync();

		await using DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_connectionString)
			.SetScriptsFolder(project.Path);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });
		await builder.CreateTester().RunAllTestsAsync();
	}

	private async Task DropAsync()
	{
		await using SqlConnection master = new(_connectionString.SetMsSqlDatabaseName("master"));
		await master.OpenAsync();
		await using SqlCommand drop = new(DropTestDatabase, master);
		_ = await drop.ExecuteNonQueryAsync();

		// the drop closed the connections of the previous run. SqlClient replaces them on Windows without this line (verified),
		// Npgsql does not; the docs clear the pool of the test database on both providers. Not ClearAllPools: it also clears
		// the pools of the other tests of the process, and their next connection in a TransactionScope fails with
		// "Implicit distributed transactions have not been enabled" (verified).
		SqlConnection.ClearPool(new SqlConnection(_connectionString));
	}

	private async Task<int> CountAsync(string query)
	{
		await using SqlConnection master = new(_connectionString.SetMsSqlDatabaseName("master"));
		await master.OpenAsync();
		await using SqlCommand cmd = new(query, master);
		return (int)(await cmd.ExecuteScalarAsync())!;
	}
}
