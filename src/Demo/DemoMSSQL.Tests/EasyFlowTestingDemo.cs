using EasyFlow;
using EasyFlow.Common;
using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL;
using EasyFlow.xunit;
using Testcontainers.MsSql;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyEasyFlowTestingMSSQLFixture: EasyFlowTestingFixture
{
	public const string SqlProjectName = "DemoMSSQL";
	public const string DockerImage = "mcr.microsoft.com/mssql/server:2022-latest";

	private readonly MsSqlContainer _dockerContainer = new MsSqlBuilder().WithImage(DockerImage).Build();

	public async override Task<IEasyFlowBuilder> GetBuilderAsync()
	{
		await _dockerContainer.StartAsync();
		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();

		// or just local sql server
		//string dbCnnString = "Server=localhost;Database=master;Trusted_Connection=True;".SetRandomDatabaseName();

		return new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetDbConnection(dbCnnString)
			.SetProjectPath(Path.GetFullPath(SqlProjectName));
	}
}

public class DBTests(ITestOutputHelper _output, MyEasyFlowTestingMSSQLFixture _fixture)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	// TODO: if there are a lot of tests they will be in the same place
	// it will be good to separate them by folders, it can be done by adding filter and creating multiple test methods.
	[SqlFact(SqlAssemblyName = MyEasyFlowTestingMSSQLFixture.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);
	}
}