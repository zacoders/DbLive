using EasyFlow.Common;
using EasyFlow.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace EasyFlow.MSSQL.xunit;

// todo: this can be common code, it should not be linked to MSSQL. Steps: remove default value for docker image, pass EasyFlowBuilder as parameter.

public class EasyFlowTestingMSSQLFixture(
	string projectRelativePath,
	string sqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest",
	string sqlServerConnectionString = ""
): IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithImage(sqlServerImage).Build();
	private readonly string _projectPath = Path.GetFullPath(projectRelativePath);
	private IEasyFlow? _deployer;

	public IEasyFlowTester? Tester { get; private set; }

	public async Task InitializeAsync()
	{
		var builder = new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetProjectPath(_projectPath);

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
