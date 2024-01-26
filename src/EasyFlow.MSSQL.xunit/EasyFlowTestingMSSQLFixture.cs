using EasyFlow.Common;
using EasyFlow.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace EasyFlow.MSSQL.xunit;

public class EasyFlowTestingMSSQLFixture(
	string projectPath,
	string sqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest"
)
: IAsyncLifetime
{
	private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder().WithImage(sqlServerImage).Build();
	public IEasyFlowTester? EasyFlowTester;

	public async Task InitializeAsync()
	{
		await _msSqlContainer.StartAsync();

		var builder = new EasyFlowBuilder()
			.LogToConsole()
			.SqlServer()
			.SetDbConnection(_msSqlContainer.GetConnectionString())
			.SetProjectPath(projectPath);

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
