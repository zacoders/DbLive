using EasyFlow.Common;
using EasyFlow.Testing;
using Xunit;

namespace EasyFlow.xunit;

public class EasyFlowTestingFixture(
	IEasyFlowBuilder _easyFlowBuilder,
	IEasyFlowDockerContainer _dockerContainer,	
	string _connectionString = ""
): IAsyncLifetime
{
	private IEasyFlow? _deployer;

	public IEasyFlowTester? Tester { get; private set; }

	public async Task InitializeAsync()
	{
		if (string.IsNullOrEmpty(_connectionString))
		{
			await _dockerContainer.StartAsync();
			string masterDbCnnString = _dockerContainer.GetConnectionString();
			_easyFlowBuilder.SetDbConnection(masterDbCnnString);
		}
		else
		{
			_easyFlowBuilder.SetDbConnection(_connectionString);
		}

		_deployer = _easyFlowBuilder.CreateDeployer();

		_deployer.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // test will be run in VS UI.
		});

		Tester = _easyFlowBuilder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		if (string.IsNullOrEmpty(_connectionString))
		{
			// do not need to drop testing db, just drop container.
			await _dockerContainer.DisposeAsync();
		}
		else
		{
			_deployer!.DropDatabase();
		}
	}
}
