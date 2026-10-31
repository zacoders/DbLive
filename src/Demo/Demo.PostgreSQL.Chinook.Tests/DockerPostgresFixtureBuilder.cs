using DbLive;
using DbLive.PostgreSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using System.Reflection;
using Testcontainers.PostgreSql;

namespace Demo.PostgreSQL.Chinook.Tests;

public class DockerPostgresFixtureBuilder : IDbLiveFixtureBuilder
{
	private static readonly PostgreSqlContainer _container =
		new PostgreSqlBuilder("postgres:latest").Build();

	public async Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_container.State != TestcontainersStates.Running)
			await _container.StartAsync().ConfigureAwait(false);

		string connectionString = _container.GetConnectionString().SetRandomPostgreSqlDatabaseName();

		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(connectionString)
			.SetProject(GetProjectAssembly());
	}

	public Assembly GetProjectAssembly() => typeof(DemoPostgreSQLChinookLink).Assembly;
}