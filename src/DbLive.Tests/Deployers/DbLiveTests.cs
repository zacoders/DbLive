namespace DbLive.Tests.Deployers;

public class DbLiveTests
{
	[Fact]
	public async Task DeployAsync()
	{
		// Arrange
		MockSet mockSet = new();

		//mockSet.DbLiveInternalDeployer.SelfDeployProjectInternal();

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = true
		};

		// Act
		await dbLive.DeployAsync(parameters);

		// Assert
		await mockSet.DbLiveDA.Received().CreateDBAsync();
		await mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}


	[Fact]
	public async Task Deploy_DoNotCreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			CreateDbIfNotExists = false
		};

		// Act
		await dbLive.DeployAsync(parameters);

		// Assert
		await mockSet.DbLiveDA.DidNotReceive().CreateDBAsync();
		await mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}


	[Fact]
	public async Task Deploy_with_RecreateDatabase()
	{
		// Arrange
		MockSet mockSet = new();

		//mockSet.DbLiveInternalDeployer.SelfDeployProjectInternal();

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		DeployParameters parameters = new()
		{
			RecreateDatabase = true
		};

		// Act
		await dbLive.DeployAsync(parameters);

		// Assert
		await mockSet.DbLiveDA.Received().DropDbAsync();
		await mockSet.DbLiveDA.Received().CreateDBAsync();
		await mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Is(parameters));
	}
}