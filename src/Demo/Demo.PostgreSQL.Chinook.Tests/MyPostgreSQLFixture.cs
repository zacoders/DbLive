using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace Demo.PostgreSQL.Chinook.Tests;

public class MyPostgreSQLFixture()
	: DbLiveTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "Demo.PostgreSQL.Chinook";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2025-latest";

	private static readonly PostgreSqlContainer _dockerContainer
		= new PostgreSqlBuilder("postgres:latest")
			.WithName("Demo.PostgreSQL.Chinook")
			//.WithReuse(true)
			.Build();

	public async override Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync().ConfigureAwait(false);
		}

		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomPostgreSqlDatabaseName();

		// or just local sql server
		//string dbCnnString = "...".SetRandomDatabaseName();

		return new DbLiveBuilder()
			.PostgreSQL()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(Path.GetFullPath(SqlProjectName));
	}
}
