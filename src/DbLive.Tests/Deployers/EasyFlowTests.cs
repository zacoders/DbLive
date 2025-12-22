namespace DbLive.Tests.Deployers;

public class DbLiveTests
{
	[Fact]
	public void DropDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		var DbLive = mockSet.CreateUsingMocks<DbLive>();

		// Act
		DbLive.DropDatabase();

		// Assert
		mockSet.DbLiveDA.Received().DropDB();
	}

	[Fact]
	public void Deploy()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveInternal.SelfDeployProjectInternal();

		var DbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = true
		};

		// Act
		DbLive.Deploy(parameters);

		// Assert
		mockSet.DbLiveDA.Received().CreateDB();
		mockSet.DbLiveInternal.Received().SelfDeployProjectInternal();
		mockSet.DbLiveInternal.Received().DeployProjectInternal(Arg.Is(false), Arg.Is(parameters));
	}


	[Fact]
	public void Deploy_DoNotCreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.DbLiveInternal.SelfDeployProjectInternal();

		var DbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = false
		};

		// Act
		DbLive.Deploy(parameters);

		// Assert
		mockSet.DbLiveDA.DidNotReceive().CreateDB();
		mockSet.DbLiveInternal.Received().SelfDeployProjectInternal();
		mockSet.DbLiveInternal.Received().DeployProjectInternal(Arg.Is(false), Arg.Is(parameters));
	}
}