using DbLive.MSSQL;
using DbLive.PostgreSQL;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Reflection;

namespace DbLive.Tests.Builder;

/// <summary>
/// The fixture builders for a server the user runs. They connect nowhere until a fixture deploys.
/// </summary>
public class FixtureBuilderTests
{
	private static readonly Assembly Project = typeof(FixtureBuilderTests).Assembly;

	[Fact]
	public async Task MsSqlFixtureBuilder_gives_every_fixture_a_new_database_on_the_server()
	{
		MsSqlFixtureBuilder fixtureBuilder = new(Project, "Server=sql1;Database=master;User ID=sa;Password=pwd;TrustServerCertificate=True;");

		using DbLiveBuilder first = await fixtureBuilder.GetBuilderAsync();
		using DbLiveBuilder second = await fixtureBuilder.GetBuilderAsync();

		SqlConnectionStringBuilder firstCnn = new(GetConnectionString(first));
		SqlConnectionStringBuilder secondCnn = new(GetConnectionString(second));
		Assert.Equal("sql1", firstCnn.DataSource);
		Assert.Equal("sa", firstCnn.UserID);
		Assert.Equal("pwd", firstCnn.Password);
		Assert.True(firstCnn.TrustServerCertificate);
		Assert.Matches("^DbLive--[0-9a-f-]{36}$", firstCnn.InitialCatalog);
		Assert.Equal("sql1", secondCnn.DataSource);
		Assert.Matches("^DbLive--[0-9a-f-]{36}$", secondCnn.InitialCatalog);
		Assert.NotEqual(firstCnn.InitialCatalog, secondCnn.InitialCatalog);

		AssertProvider(DbProvider.MsSql, first);
		await AssertProjectAsync(fixtureBuilder, first);
	}

	[Fact]
	public async Task PostgreSqlFixtureBuilder_gives_every_fixture_a_new_database_on_the_server()
	{
		PostgreSqlFixtureBuilder fixtureBuilder = new(Project, "Host=pg1;Port=5433;Database=postgres;Username=postgres;Password=pwd");

		using DbLiveBuilder first = await fixtureBuilder.GetBuilderAsync();
		using DbLiveBuilder second = await fixtureBuilder.GetBuilderAsync();

		NpgsqlConnectionStringBuilder firstCnn = new(GetConnectionString(first));
		NpgsqlConnectionStringBuilder secondCnn = new(GetConnectionString(second));
		Assert.Equal("pg1", firstCnn.Host);
		Assert.Equal(5433, firstCnn.Port);
		Assert.Equal("postgres", firstCnn.Username);
		Assert.Equal("pwd", firstCnn.Password);
		Assert.Matches("^dblive--[0-9a-f-]{36}$", firstCnn.Database);
		Assert.Equal("pg1", secondCnn.Host);
		Assert.Matches("^dblive--[0-9a-f-]{36}$", secondCnn.Database);
		Assert.NotEqual(firstCnn.Database, secondCnn.Database);

		AssertProvider(DbProvider.PostgreSql, first);
		await AssertProjectAsync(fixtureBuilder, first);
	}

	private static string GetConnectionString(DbLiveBuilder builder) =>
		builder.GetRequiredService<IDbLiveDbConnection>().ConnectionString;

	private static void AssertProvider(DbProvider expected, DbLiveBuilder builder)
	{
		Assert.Equal(expected, builder.GetRequiredService<IDbLiveDA>().Provider);
	}

	private static async Task AssertProjectAsync(IDbLiveFixtureBuilder fixtureBuilder, DbLiveBuilder builder)
	{
		Assert.Same(Project, fixtureBuilder.GetProjectAssembly());

		TestItem test = Assert.Single(await builder.CreateProject().GetTestsAsync());
		Assert.Equal("Tests/example.sql", test.FileData.RelativePath);
	}
}
