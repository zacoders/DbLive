namespace DbLive.Tests.Deployers;

public class DbLiveDeployerTests
{
	[Fact]
	public async Task Deploy_executes_all_steps_in_correct_order()
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

		mockSet.AppliedMigrationsValidator
			.When(x => x.ValidateAsync())
			.Do(_ => calls.Add("applied-versions"));

		mockSet.MigrationChecksumValidator
			.When(x => x.ValidateAsync(parameters))
			.Do(_ => calls.Add("checksum"));

		mockSet.MigrationsSaver
			.When(x => x.SaveAsync())
			.Do(_ => calls.Add("save"));

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

		DbLiveDeployer deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		await deployer.DeployAsync(parameters);

		// assert – exact order
		await mockSet.TransactionSettingsValidator.Received(1).ValidateAsync(Arg.Any<DbLiveSettings>());
		Assert.Equal(
			[
				"applied-versions",
				"checksum",
				"save",
				"folder-before",
				"migrations",
				"code",
				"breaking",
				"folder-after"
			],
			calls
		);

		// the SQL tests are not a step of the deployment: IDbLiveTester.RunAllTestsAsync runs them.
		await mockSet.UnitTestsRunner.DidNotReceive().RunAllTestsAsync();
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
		mockSet.Logger.Received(1).Information("Starting project deploy from {Location}.", "memory");
		mockSet.Logger.Received(1).Information("Project deploy completed.");
	}

	[Theory]
	[MemberData(nameof(AllDeployParameters))]
	public async Task Database_ahead_of_project_stops_the_deployment_before_any_change(DeployParameters parameters)
	{
		// arrange: an older build, the database has versions 7 and 8 that the project does not have.
		MockSet mockSet = new();

		mockSet.AppliedMigrationsValidator
			.ValidateAsync()
			.Returns(Task.FromException(new DatabaseAheadOfProjectException([7, 8])));

		DbLiveDeployer deployer = mockSet.CreateUsingMocks<DbLiveDeployer>();

		// act
		DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(() => deployer.DeployAsync(parameters));

		// assert
		Assert.Equal([7, 8], ex.Versions);
		await mockSet.MigrationChecksumValidator.DidNotReceiveWithAnyArgs().ValidateAsync(default!);
		await mockSet.MigrationsSaver.DidNotReceive().SaveAsync();
		await mockSet.FolderDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default, default!);
		await mockSet.MigrationsDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default!);
		await mockSet.CodeDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default!);
		await mockSet.BreakingChangesDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default!);
	}

	public static TheoryData<DeployParameters> AllDeployParameters() =>
	[
		DeployParameters.Default,
		// an older build must not run the breaking phase either.
		DeployParameters.Breaking
	];
}
