using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace Demo.PostgreSQL.Chinook.Tests;

public class MyPostgreSQLFixture() 
	: DbLiveTestFixtureBase(dropDatabaseOnComplete: true)
{
	private static readonly PostgreSqlContainer _dockerContainer
		= new PostgreSqlBuilder("postgres:latest")
			.WithName("Demo.PostgreSQL.Chinook")
			.Build();

	public override string GetProjectPath()
	{
		return Path.GetFullPath("Demo.PostgreSQL.Chinook");
	}

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
			.SetProjectPath(GetProjectPath());
	}
}
