using EasyFlow;
using EasyFlow.Common;
using EasyFlow.MSSQL;
using EasyFlow.Testing;
using Testcontainers.MsSql;
using Xunit;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;

public class EasyFlowTestingMSSQLTheoryData : TheoryData<string>
{
	readonly static string ProjectPath = Path.GetFullPath(@"DemoMSSQL");

	public EasyFlowTestingMSSQLTheoryData()
	{
		var project = new EasyFlowBuilder()
			.SetProjectPath(ProjectPath)
			.CreateProject();

		foreach (var testItem in project.GetTests())
		{
			Add(testItem.FileData.RelativePath); // adding tests to TheoryData base class.
		}
	}
}

public class EasyFlowTestingMSSQLFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().Build();
	public IEasyFlowTester? EasyFlowTester;

	public async Task InitializeAsync()
	{
		await _msSqlContainer.StartAsync();

		var builder = new EasyFlowBuilder()
			//.LogToXUnitOutput() //todo: need logger for test output
			.LogToConsole()
			.SqlServer()
			.SetDbConnection(_msSqlContainer.GetConnectionString())
			.SetProjectPath(Path.GetFullPath(@"DemoMSSQL"));

		var easyFlow = builder.CreateDeployer();

		easyFlow.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false
		});

		EasyFlowTester = builder.CreateTester();
	}

	public Task DisposeAsync() => _msSqlContainer.DisposeAsync().AsTask();

}

public class EasyFlowTestingDemo(EasyFlowTestingMSSQLFixture _fixture, ITestOutputHelper _output)
	: IClassFixture<EasyFlowTestingMSSQLFixture>
{
	[Theory]
	[ClassData(typeof(EasyFlowTestingMSSQLTheoryData))]
	public void Sql(string relativePath) //=> _fixture.RunTest(_output, relativePath);\
	{
		_fixture.EasyFlowTester!.RunTest(_output.WriteLine, relativePath);
	}
}
