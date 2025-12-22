using DbLive;
using DbLive.Common;
using DbLive.Deployers.Testing;
using DbLive.MSSQL;
using DbLive.xunit;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


public class MyDbLiveTestingMSSQLFixture()
	: DbLiveTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "AdventureWorks.Database";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2022-latest";

	private readonly MsSqlContainer _dockerContainer = new MsSqlBuilder().WithImage(DockerImage).Build();

	public async override Task<IDbLiveBuilder> GetBuilderAsync()
	{
		await _dockerContainer.StartAsync();
		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		// or just local sql server
		//string dbCnnString = "Server=localhost;Database=master;Trusted_Connection=True;".SetRandomDatabaseName();
		string projectPath = Path.GetFullPath(SqlProjectName);
		return new DbLiveBuilder()
			.LogToConsole()
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);
	}
}

public class DBTests(ITestOutputHelper _output, MyDbLiveTestingMSSQLFixture _fixture)
	: IClassFixture<MyDbLiveTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = MyDbLiveTestingMSSQLFixture.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}