namespace DbLive.Tests.Deployers;

public class DbLiveTests
{
	[Fact]
	public void Deploy()
	{
		// Arrange
		MockSet mockSet = new();

		//mockSet.DbLiveInternalDeployer.SelfDeployProjectInternal();

		var dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = true
		};

		// Act
		dbLive.DeployAsync(parameters);

		// Assert
		mockSet.DbLiveDA.Received().CreateDBAsync();
		mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}


	[Fact]
	public void Deploy_DoNotCreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		var dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = false
		};

		// Act
		dbLive.DeployAsync(parameters);

		// Assert
		mockSet.DbLiveDA.DidNotReceive().CreateDBAsync();
		mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}


	[Fact]
	public void Deploy_with_RecreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		//mockSet.DbLiveInternalDeployer.SelfDeployProjectInternal();

		var dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			RecreateDatabase = true
		};

		// Act
		dbLive.DeployAsync(parameters);

		// Assert
		mockSet.DbLiveDA.Received().DropDBAsync();
		mockSet.DbLiveDA.Received().CreateDBAsync();
		mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}
}