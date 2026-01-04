
namespace DbLive.Tests.Deployers;

public class DbLiveDeployerTests
{
	[Fact]
	public void Deploy_executes_all_steps_in_correct_order_and_runs_tests_after_transaction()
	{
		// arrange
		MockSet mockSet = new();

		var parameters = DeployParameters.Default;

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Deployment,
			TransactionIsolationLevel = TranIsolationLevel.ReadCommitted,
			DeploymentTimeout = TimeSpan.FromMinutes(5)
		});

		List<string> calls = [];

		mockSet.DowngradeDeployer
			.When(x => x.Deploy(parameters))
			.Do(_ => calls.Add("downgrade"));

		mockSet.FolderDeployer
			.When(x => x.Deploy(ProjectFolder.BeforeDeploy, parameters))
			.Do(_ => calls.Add("folder-before"));

		mockSet.MigrationsDeployer
			.When(x => x.Deploy(parameters))
			.Do(_ => calls.Add("migrations"));

		mockSet.CodeDeployer
			.When(x => x.Deploy(parameters))
			.Do(_ => calls.Add("code"));

		mockSet.BreakingChangesDeployer
			.When(x => x.Deploy(parameters))
			.Do(_ => calls.Add("breaking"));

		mockSet.FolderDeployer
			.When(x => x.Deploy(ProjectFolder.AfterDeploy, parameters))
			.Do(_ => calls.Add("folder-after"));

		mockSet.UnitTestsRunner
			.When(x => x.RunAllTests(parameters))
			.Do(_ => calls.Add("tests"));

		var deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		deployer.Deploy(parameters);

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
	public void Deploy_logs_start_and_completion()
	{
		// arrange
		MockSet mockSet = new();

		var parameters = DeployParameters.Default;

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings());

		var deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		deployer.Deploy(parameters);

		// assert
		mockSet.Logger.Received(1).Information("Starting project deploy.");
		mockSet.Logger.Received(1).Information("Project deploy completed.");
	}

}
