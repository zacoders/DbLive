namespace DbLive.Tests.Deployers;

public class DbLiveSelfDeployerTests
{
	[Fact(Skip = "Hard to mock")]
	public void SelfDeployProjectInternal_logs_and_deploys_with_expected_parameters()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings
		{
			LogSelfDeploy = true
		});

		var dbLive = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		mockSet.DbLiveBuilder.CloneBuilder().Returns(mockSet.DbLiveBuilder);

		// Act
		dbLive.Deploy();

		// Assert
		mockSet.Logger.Received(1).Information("Starting self deploy.");
		mockSet.Logger.Received(1).Information("Self deploy completed.");

		mockSet.DbLiveInternalDeployer.Received(1).Deploy(
			true,
			Arg.Is<DeployParameters>(p =>
				p.CreateDbIfNotExists == false &&
				p.DeployBreaking == false &&
				p.DeployCode == true &&
				p.DeployMigrations == true &&
				p.RunTests == false
			)
		);
	}

	[Fact(Skip = "Hard to mock")]
	public void SelfDeployProjectInternal_disables_logging_and_uses_no_logs_builder()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings
		{
			LogSelfDeploy = false
		});

		mockSet.DbLiveBuilder.CloneBuilder().Returns(mockSet.DbLiveBuilder);

		var dbLive = mockSet.CreateUsingMocks<DbLiveSelfDeployer>();

		// Act
		dbLive.Deploy();

		// Assert
		mockSet.Logger.DidNotReceive().Information(Arg.Any<string>());

		mockSet.DbLiveInternalDeployer.Received(1)
			.Deploy(
				true,
				Arg.Any<DeployParameters>()
			);
	}
}