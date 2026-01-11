using DbLive;
using DbLive.Common;
using DbLive.Deployers.Testing;
using DbLive.PostgreSQL;
using DbLive.xunit;
using DbLive.xunit.SqlTest;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace Demo.PostgreSQL.Chinook.Tests;

public class MyPostgreSQLFixture()
	: DbLiveTestFixtureBase(dropDatabaseOnComplete: true)
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


public class SqlTests(ITestOutputHelper _output, MyPostgreSQLFixture _fixture)
	: IClassFixture<MyPostgreSQLFixture>
{
	[SqlFact(TestFixture = typeof(MyPostgreSQLFixture))]
	public async Task Sql(string testFileRelativePath)
	{
		TestRunResult result = await _fixture.Tester!.RunTestAsync(_output.WriteLine, testFileRelativePath).ConfigureAwait(false);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}
