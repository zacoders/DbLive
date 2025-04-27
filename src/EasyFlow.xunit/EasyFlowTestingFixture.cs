using EasyFlow.Common;
using EasyFlow.Testing;
using Xunit;

namespace EasyFlow.xunit;

public abstract class EasyFlowTestingFixture (bool dropDatabaseOnComplete)
	: IAsyncLifetime
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

	public Task DisposeAsync()
	{
		if (dropDatabaseOnComplete) _deployer!.DropDatabase();

		return Task.CompletedTask;
	}
}
