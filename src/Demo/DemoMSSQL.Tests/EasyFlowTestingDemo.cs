using EasyFlow;
using EasyFlow.Common;
using EasyFlow.Deployers.Testing;
using EasyFlow.MSSQL;
using EasyFlow.xunit;
using Testcontainers.MsSql;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public class TestConstants
{
	public const string SqlProjectName = "DemoMSSQL";

	public static IEasyFlowBuilder Builder => 
		new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetProjectPath(Path.GetFullPath(SqlProjectName));
}

internal class EasyFlowDockerContainer(string dockerImage = "mcr.microsoft.com/mssql/server:2022-latest") 
	: IEasyFlowDockerContainer
{
	private readonly MsSqlContainer _dockerContainer = new MsSqlBuilder().WithImage(dockerImage).Build();

	public async Task StartAsync() => await _dockerContainer.StartAsync();

	public string GetConnectionString()
	{
		string masterDbCnnString = _dockerContainer.GetConnectionString();
		string dbCnnString = masterDbCnnString.SetRandomDatabaseName();
		return dbCnnString;
	}

	public async Task DisposeAsync() => await _dockerContainer.DisposeAsync();
}


public class MyEasyFlowTestingMSSQLFixture() 
	: EasyFlowTestingFixture(TestConstants.Builder, new EasyFlowDockerContainer())
{ 
}

public class DBTests(ITestOutputHelper _output, MyEasyFlowTestingMSSQLFixture _fixture)
	: IClassFixture<MyEasyFlowTestingMSSQLFixture>
{
	[SqlFact(SqlAssemblyName = TestConstants.SqlProjectName)]
	public void Sql(string testFileRelativePath)
	{
		TestRunResult result = _fixture.Tester!.RunTest(_output.WriteLine, testFileRelativePath);
		Assert.True(result.IsSuccess, result.ErrorMessage);		
	}
}