using DbLive;
using DbLive.Common;
using DbLive.MSSQL;
using DbLive.xunit;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace AdventureWorks.Tests;

public class MyDbLiveTestingMSSQLFixture()
	: DbLiveTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "AdventureWorks.Database";

	private static readonly MsSqlContainer _dockerContainer
		= new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
			.WithName("DbLive.AdventureWorks")
			//.WithReuse(true)
			.Build();

	public async override Task<DbLiveBuilder> GetBuilderAsync()
	{
		if (_dockerContainer.State != TestcontainersStates.Running)
		{
			await _dockerContainer.StartAsync();
		}

		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		// or just local sql server
		//string dbCnnString = "Server=localhost;Database=master;Trusted_Connection=True;".SetRandomDatabaseName();
		string projectPath = Path.GetFullPath(SqlProjectName);
		return new DbLiveBuilder()
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);
	}
}
