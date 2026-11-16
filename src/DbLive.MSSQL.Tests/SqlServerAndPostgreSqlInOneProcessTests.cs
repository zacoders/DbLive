using Dapper;
using DbLive.Adapter;
using DbLive.PostgreSQL;
using DbLive.Project;
using DbLive.Testing;
using System.Diagnostics.CodeAnalysis;
using Testcontainers.PostgreSql;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;

/// <summary>
/// Deployments to SQL Server and to PostgreSQL in one process: a test project with fixtures for both engines, a deployer of a product
/// with a database on each. The settings of Dapper are global for the process, and the providers change none of them: a
/// <see cref="DateTime"/> sent to PostgreSQL as <c>datetime2</c> is a <c>timestamp without time zone</c>, and Npgsql rejects a UTC value for it.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class SqlServerAndPostgreSqlInOneProcessTests(SqlServerIntegrationFixture _fixture, ITestOutputHelper _output)
	: IAssemblyFixture<SqlServerIntegrationFixture>
{
	private static readonly DeployParameters CreateAndDeploy = DeployParameters.Default with { CreateDbIfNotExists = true };

	[Fact]
	public async Task PostgreSql_deploys_after_SQL_Server()
	{
		using TempProject msSqlProject = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table dbo.t1 ( id int not null );")
			.AddFile("Code/dbo/v1.sql", "create or alter view dbo.v1 as select 1 as x;")
			.AddFile("Tests/t1.sql", "select 1 as x;")
			.AddFile("AfterDeploy/a.sql", "select 1 as x;");

		DbLiveBuilder msSql = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.SqlServer()
			.SetDbConnection(_fixture.MasterDbConnectionString.SetRandomMsSqlDatabaseName())
			.SetScriptsFolder(msSqlProject.Path);

		await msSql.CreateDeployer().DeployAsync(CreateAndDeploy);
		await msSql.CreateTester().RunAllTestsAsync();

		await using PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18").Build();
		await container.StartAsync();

		using TempProject pgProject = new TempProject()
			.AddFile("Migrations/001.migration.sql", "create table t1 ( id int not null );")
			.AddFile("Code/v1.sql", "create or replace view v1 as select 1 as x;")
			.AddFile("Tests/t1.sql", "select 1 as x;")
			.AddFile("AfterDeploy/a.sql", "select 1 as x;");

		DbLiveBuilder pg = new DbLiveBuilder()
			.LogToXUnitOutput(_output)
			.PostgreSQL()
			.SetDbConnection(container.GetConnectionString().SetRandomPostgreSqlDatabaseName())
			.SetScriptsFolder(pgProject.Path);

		await pg.CreateDeployer().DeployAsync(CreateAndDeploy);
		await pg.CreateTester().RunAllTestsAsync();

		// the rows are read by the names of their columns, which the queries give.
		IDbLiveDA da = pg.GetRequiredService<IDbLiveDA>();
		Assert.Equal(
			"migrations/001.migration.sql",
			Assert.Single(await da.GetMigrationsAsync(), m => m.ItemType == MigrationItemType.Migration).RelativePath
		);
		Assert.Equal("code/v1.sql", Assert.Single(await da.GetCodeItemsAsync()).RelativePath);

		Assert.False(DefaultTypeMap.MatchNamesWithUnderscores);
	}
}
