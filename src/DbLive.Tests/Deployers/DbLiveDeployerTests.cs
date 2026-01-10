namespace DbLive.Tests.Deployers;

public class DbLiveDeployerTests
{
	[Fact]
	public async Task Deploy_executes_all_steps_in_correct_order_and_runs_tests_after_transaction()
	{
		// arrange
		MockSet mockSet = new();

		DeployParameters parameters = DeployParameters.Default;

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Deployment,
			TransactionIsolationLevel = TranIsolationLevel.ReadCommitted,
			DeploymentTimeout = TimeSpan.FromMinutes(5)
		});

		List<string> calls = [];

		mockSet.DowngradeDeployer
			.When(x => x.DeployAsync(parameters))
			.Do(_ => calls.Add("downgrade"));

		mockSet.FolderDeployer
			.When(x => x.DeployAsync(ProjectFolder.BeforeDeploy, parameters))
			.Do(_ => calls.Add("folder-before"));

		mockSet.MigrationsDeployer
			.When(x => x.DeployAsync(parameters))
			.Do(_ => calls.Add("migrations"));

		mockSet.CodeDeployer
			.When(x => x.DeployAsync(parameters))
			.Do(_ => calls.Add("code"));

		mockSet.BreakingChangesDeployer
			.When(x => x.DeployAsync(parameters))
			.Do(_ => calls.Add("breaking"));

		mockSet.FolderDeployer
			.When(x => x.DeployAsync(ProjectFolder.AfterDeploy, parameters))
			.Do(_ => calls.Add("folder-after"));

		mockSet.UnitTestsRunner
			.When(x => x.RunAllTestsAsync(parameters))
			.Do(_ => calls.Add("tests"));

		DbLiveDeployer deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		await deployer.DeployAsync(parameters);

		// assert – exact order
		Assert.Equal(
			[
				"downgrade",
				"folder-before",
				"migrations",
				"code",
				"breaking",
				"folder-after",
				"tests"
			],
			calls
		);
	}


	[Fact]
	public async Task Deploy_logs_start_and_completion()
	{
		// arrange
		MockSet mockSet = new();

		DeployParameters parameters = DeployParameters.Default;

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		DbLiveDeployer deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		await deployer.DeployAsync(parameters);

		// assert
		mockSet.Logger.Received(1).Information("Starting project deploy.");
		mockSet.Logger.Received(1).Information("Project deploy completed.");
	}

}
