using DbLive;
using DbLive.MSSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using System.Reflection;
using Testcontainers.MsSql;

namespace DemoMSSQL.Tests;


public class DockerMsSqlFixtureBuilder : IDbLiveFixtureBuilder
{
	private static readonly MsSqlContainer _container =
		new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

	public async Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_container.State != TestcontainersStates.Running)
			await _container.StartAsync().ConfigureAwait(false);

		string connectionString = _container.GetConnectionString().SetRandomMsSqlDatabaseName();

		return new DbLiveBuilder()
			.SqlServer()
			.SetDbConnection(connectionString)
			.SetProject(GetProjectAssembly());
	}

	public Assembly GetProjectAssembly() => Assembly.Load("DemoMSSQL");
}

