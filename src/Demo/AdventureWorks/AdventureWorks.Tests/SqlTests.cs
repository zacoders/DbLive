using EasyFlow;
using EasyFlow.Common;
using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL;
using EasyFlow.xunit;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


public class MyEasyFlowTestingMSSQLFixture()
	: EasyFlowTestingFixture(dropDatabaseOnComplete: true)
{
	public const string SqlProjectName = "AdventureWorks.Database";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2022-latest";

	private readonly MsSqlContainer _dockerContainer = new MsSqlBuilder().WithImage(DockerImage).Build();

	public async override Task<IEasyFlowBuilder> GetBuilderAsync()
	{
		await _dockerContainer.StartAsync();
		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		// or just local sql server
		//string dbCnnString = "Server=localhost;Database=master;Trusted_Connection=True;".SetRandomDatabaseName();
		string projectPath = Path.GetFullPath(SqlProjectName);
		return new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(projectPath);
	}
}

public class DBTests(ITestOutputHelper _output, MyEasyFlowTestingMSSQLFixture _fixture)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = MyEasyFlowTestingMSSQLFixture.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}