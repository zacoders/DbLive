using DbLive;
using DbLive.Common;
using DbLive.PostgreSQL;
using DbLive.xunit.Deploy;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;

public class MyDeployFixtureBase() : DeployFixtureBase(dropDatabaseOnComplete: true)
{
	private static readonly PostgreSqlContainer _dockerContainer
		= new PostgreSqlBuilder("postgres:latest").Build();

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


public class DeploymentTests(ITestOutputHelper _output, MyDeployFixtureBase fixture)
	: IClassFixture<MyDeployFixtureBase>
{
	[SqlDeployTest]
	public async Task Deploy(bool deployBreaking, UndoTestMode undoTestMode)
	{		
		await fixture.DeployAsync(_output, deployBreaking, undoTestMode).ConfigureAwait(false);
	}
}
