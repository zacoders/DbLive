using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.PostgreSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class MissingDatabaseTests(PostgreSqlFixture fixture, ITestOutputHelper output)
	: PostgreSqlDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Deployment_to_a_database_that_does_not_exist_fails_with_its_name()
	{
		using TempProject project = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 (id int not null);");

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			() => CreateBuilder(project).CreateDeployer().DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(
			$"Database '{DbName}' does not exist on server '{new NpgsqlConnectionStringBuilder(DbCnnString).Host}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			ex.Message
		);
		Assert.Equal(PostgresErrorCodes.InvalidCatalogName, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
		Assert.False(await DbExistsAsync());
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
}
