namespace DbLive.Tests.Deployers;

public class DbLiveTests
{
	[Fact]
	public void DropDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		var dbLive = mockSet.CreateUsingMocks<DbLive>();

		// Act
		dbLive.DropDatabase();

		// Assert
		mockSet.DbLiveDA.Received().DropDB();
	}

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
		dbLive.Deploy(parameters);

		// Assert
		mockSet.DbLiveDA.Received().CreateDB();
		mockSet.DbLiveSelfDeployer.Received().Deploy();
		mockSet.DbLiveInternalDeployer.Received().Deploy(Arg.Is(parameters));
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
		dbLive.Deploy(parameters);

		// Assert
		mockSet.DbLiveDA.DidNotReceive().CreateDB();
		mockSet.DbLiveSelfDeployer.Received().Deploy();
		mockSet.DbLiveInternalDeployer.Received().Deploy(Arg.Is(parameters));
	}
}