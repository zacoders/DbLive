using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace AdventureWorks.Tests;


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
			.SetProjectPath(GetProjectPath());
	}

	public string GetProjectPath() => Path.GetFullPath("AdventureWorks.Database");
}

