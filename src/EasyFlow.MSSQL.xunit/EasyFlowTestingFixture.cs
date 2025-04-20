using EasyFlow.Common;
using EasyFlow.Testing;
using Xunit;

namespace EasyFlow.xunit;

public abstract class EasyFlowTestingFixture: IAsyncLifetime
{
	private IEasyFlow? _deployer;

	public IEasyFlowTester? Tester { get; private set; }

	public abstract Task<IEasyFlowBuilder> GetBuilderAsync();

	public async Task InitializeAsync()
	{
		IEasyFlowBuilder easyFlowBuilder = await GetBuilderAsync();

		_deployer = easyFlowBuilder.CreateDeployer();

		_deployer.Deploy(new DeployParameters
		{
			CreateDbIfNotExists = true,
			DeployBreaking = true,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = false // do not need to run tests, they will be run in VS UI.
		});

		Tester = easyFlowBuilder.CreateTester();
	}

	public async Task DisposeAsync()
	{
		//if (string.IsNullOrEmpty(_connectionString))
		//{
		//	// do not need to drop testing db, just drop container.
		//	await _dockerContainer.DisposeAsync();
		//}
		//else
		{
			_deployer!.DropDatabase();
		}
	}
}
