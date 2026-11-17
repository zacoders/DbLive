using DbLive.Adapter;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MissingDatabaseTests(PostgreSqlFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<PostgreSqlFixture>
{
	private readonly string _dbCnnString = _fixture.PostgresDBConnectionString.SetRandomPostgreSqlDatabaseName();

	private string DbName => new NpgsqlConnectionStringBuilder(_dbCnnString).Database!;

	[Fact]
	public async Task Deployment_to_a_database_that_does_not_exist_fails_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(
			$"Database '{DbName}' does not exist on server '{new NpgsqlConnectionStringBuilder(_dbCnnString).Host}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			ex.Message
		);
		Assert.Equal(PostgresErrorCodes.InvalidCatalogName, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
		Assert.False(await DatabaseExistsAsync());
	}

	[Fact]
	public async Task Tests_against_a_database_that_does_not_exist_fail_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Tests/t1.test.sql", "select 'empty' as assert;");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			CreateBuilder(project).CreateTester().RunAllTestsAsync
		);

		Assert.Equal(DbName, ex.DatabaseName);
	}

	private async Task<bool> DatabaseExistsAsync()
	{
		using NpgsqlConnection cnn = new(_fixture.PostgresDBConnectionString);
		await cnn.OpenAsync();
		using NpgsqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select exists(select 1 from pg_database where datname = @name)";
		_ = cmd.Parameters.AddWithValue("name", DbName);
		return (bool)(await cmd.ExecuteScalarAsync())!;
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
