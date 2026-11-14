using DbLive.xunit;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


/// <summary>
/// The recipe for the tests without xUnit on a server of your own ("Running all tests without xUnit" in
/// docs/unit-testing/index.md, Section 8.2 of docs/llms-full.txt): a database with a fixed name, dropped with the SQL of the
/// docs before the deployment. Two runs in one process: the connections of the first one stay in the pool.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class TestsWithoutXunitRecipeTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	// the SQL of the docs, as it is there.
	private const string DropTestDatabase = "drop database if exists mydb_tests with (force);";

	private readonly string _connectionString = _fixture.PostgresDBConnectionString.SetPostgreSqlDatabaseName("mydb_tests");

	[Fact]
	public async Task The_next_run_drops_the_database_while_the_connections_of_the_previous_run_use_it()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table person (id int not null);")
			.AddFile("Tests/no-person.sql", "select id from person; select 'empty' as assert;");

		await RunRecipeAsync(project);

		// data that the next run must not see, written on a connection that stays in the pool as well.
		await using (NpgsqlConnection cnn = new(_connectionString))
		{
			await cnn.OpenAsync();
			await using NpgsqlCommand insert = new("insert into person (id) values (1);", cnn);
			_ = await insert.ExecuteNonQueryAsync();
		}
		Assert.True(await CountAsync("select count(*) from pg_stat_activity where datname = 'mydb_tests'") > 0);

		await RunRecipeAsync(project);

		await DropAsync();
		Assert.Equal(0, await CountAsync("select count(*) from pg_database where datname = 'mydb_tests'"));
	}

	// the recipe of the docs, with the scripts of a folder instead of a SQL project.
	private async Task RunRecipeAsync(TempProject project)
	{
		await DropAsync();

		await using DbLiveBuilder builder = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.PostgreSQL()
			.SetDbConnection(_connectionString)
			.SetScriptsFolder(project.Path);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });
		await builder.CreateTester().RunAllTestsAsync();
	}

	private async Task DropAsync()
	{
		await using NpgsqlConnection postgres = new(_connectionString.SetPostgreSqlDatabaseName("postgres"));
		await postgres.OpenAsync();
		await using NpgsqlCommand drop = new(DropTestDatabase, postgres);
		_ = await drop.ExecuteNonQueryAsync();

		// the drop closed the connections of the previous run: without this the deployment gets one from the pool and fails
		// with "57P01: terminating connection due to administrator command" (verified). The pool of the test database only.
		NpgsqlConnection.ClearPool(new NpgsqlConnection(_connectionString));
	}

	private async Task<long> CountAsync(string query)
	{
		await using NpgsqlConnection postgres = new(_connectionString.SetPostgreSqlDatabaseName("postgres"));
		await postgres.OpenAsync();
		await using NpgsqlCommand cmd = new(query, postgres);
		return (long)(await cmd.ExecuteScalarAsync())!;
	}
}
