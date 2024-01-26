using EasyFlow.Common;
using EasyFlow.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace EasyFlow.MSSQL.xunit;

public class EasyFlowTestingMSSQLFixture(
	string projectPath,
	string sqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest",
	string sqlServerConnectionString = ""
)
: EasyFlowTestingMSSQLTheoryData(projectPath), IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithImage(sqlServerImage).Build();
	private IEasyFlow? _deployer;
	
	public IEasyFlowTester? Tester { get; private set; }
	
	public async Task InitializeAsync()
	{		
		var builder = new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetProjectPath(projectPath);

		if (string.IsNullOrEmpty(sqlServerConnectionString))
		{
			await _msSqlContainer.StartAsync();
			string masterDbCnnString = _msSqlContainer.GetConnectionString();
			string dbCnnString = masterDbCnnString.SetRandomDatabaseName();
			builder.SetDbConnection(dbCnnString);
		}
		else
		{
			builder.SetDbConnection(sqlServerConnectionString);
		}

		_deployer = builder.CreateDeployer();

		_deployer.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // test will be run in VS UI.
		});

		Tester = builder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		if (string.IsNullOrEmpty(sqlServerConnectionString))
		{
			// do not need to drop testing db, just drop container.
			await _msSqlContainer.DisposeAsync().AsTask();
		}
		else
		{
			_deployer!.DropDatabase();
		}
	}
}
