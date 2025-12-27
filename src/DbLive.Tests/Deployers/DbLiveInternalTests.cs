namespace DbLive.Tests.Deployers;

public class DbLiveInternalTests
{
	[Fact]
	public void SelfDeployProjectInternal()
	{
		// Arrange
		MockSet mockSet = new();

		DeployParameters selfDeployParameters = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = false,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true
		};

		var DbLive = mockSet.CreateUsingMocks<DbLiveInternal>();

		mockSet.SelfDeployer.CreateDbLiveInternal().Returns(mockSet.DbLiveInternal);

		// Act
		DbLive.SelfDeployProjectInternal();

		// Assert
		mockSet.SelfDeployer.Received().CreateDbLiveInternal();
		mockSet.DbLiveInternal.Received().DeployProjectInternal(Arg.Is(true), Arg.Is(selfDeployParameters));
	}


	[Fact]
	public void DeployProjectInternal_ProjectSettingsTest()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Deployment,
			TransactionIsolationLevel = TranIsolationLevel.RepeatableRead,
			DeploymentTimeout = TimeSpan.FromHours(5)
		});

		var DbLive = mockSet.CreateUsingMocks<DbLiveInternal>();

		bool isSelfDeploy = true;

		// Act
		DbLive.DeployProjectInternal(isSelfDeploy, DeployParameters.Default);

		// Assert
		mockSet.TransactionRunner.Received()
			.ExecuteWithinTransaction(
				Arg.Is(true),
				Arg.Is(TranIsolationLevel.RepeatableRead),
				Arg.Is(TimeSpan.FromHours(5)),
				Arg.Any<Action>()
			);
	}


	[Fact]
	public void DeployProjectInternal_ProjectSettingsTest2()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.ProjectSettings.Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Migration,
			TransactionIsolationLevel = TranIsolationLevel.Chaos,
			DeploymentTimeout = TimeSpan.FromHours(15)
		});

		var DbLive = mockSet.CreateUsingMocks<DbLiveInternal>();

		bool isSelfDeploy = true;

		// Act
		DbLive.DeployProjectInternal(isSelfDeploy, DeployParameters.Default);

		// Assert
		mockSet.TransactionRunner.Received()
			.ExecuteWithinTransaction(
				Arg.Is(false),
				Arg.Is(TranIsolationLevel.Chaos),
				Arg.Is(TimeSpan.FromHours(15)),
				Arg.Any<Action>()
			);
	}


	[Fact]
	public void DeployProjectInternal()
	{
		// Arrange
		MockSet mockSet = new();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = true,
			DeployBreaking = false,
			DeployCode = true,
			DeployMigrations = true,
			RunTests = true
		};

		var DbLive = mockSet.CreateUsingMocks<DbLiveInternal>();

		bool isSelfDeploy = true;

		// Act
		DbLive.DeployProjectInternal(isSelfDeploy, parameters);

		// Assert
		mockSet.FolderDeployer.Received().DeployFolder(Arg.Is(ProjectFolder.BeforeDeploy), Arg.Is(parameters));
		mockSet.MigrationsDeployer.Received().DeployMigrations(Arg.Is(isSelfDeploy), Arg.Is(parameters));
		mockSet.CodeDeployer.Received().DeployCode(Arg.Is(isSelfDeploy), Arg.Is(parameters));
		mockSet.BreakingChangesDeployer.Received().DeployBreakingChanges(Arg.Is(parameters));
		mockSet.FolderDeployer.Received().DeployFolder(Arg.Is(ProjectFolder.AfterDeploy), Arg.Is(parameters));
		mockSet.UnitTestsRunner.Received().RunAllTests(Arg.Is(parameters));
	}
}