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
		await mockSet.DbLiveDA.DidNotReceive().CheckDatabaseExistsAsync();
		await mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Any<ProjectSnapshot>(), Arg.Is(parameters));
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
		await mockSet.DbLiveDA.Received(1).CheckDatabaseExistsAsync();
		await mockSet.DbLiveSelfDeployer.Received().DeployAsync();
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Any<ProjectSnapshot>(), Arg.Is(parameters));
	}

	[Fact]
	public async Task Deploy_stops_when_the_database_does_not_exist()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.CheckDatabaseExistsAsync()
			.Returns(Task.FromException(new DatabaseNotFoundException("MyDb", "localhost", new Exception("cannot open"))));

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		DatabaseNotFoundException ex = await Assert.ThrowsAsync<DatabaseNotFoundException>(
			() => dbLive.DeployAsync(DeployParameters.Default)
		);

		Assert.Equal(
			"Database 'MyDb' does not exist on server 'localhost'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			ex.Message
		);
		await mockSet.DbLiveSelfDeployer.DidNotReceive().DeployAsync();
		await mockSet.DbLiveInternalDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!);
	}

	[Fact]
	public async Task ProjectId_is_checked_before_the_self_deployment_and_bound_after_it()
	{
		MockSet mockSet = new();

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		await dbLive.DeployAsync(DeployParameters.Default);

		Received.InOrder(() =>
		{
			mockSet.ProjectIdValidator.ValidateAsync();
			mockSet.DbLiveSelfDeployer.DeployAsync();
			mockSet.ProjectIdValidator.BindAsync();
			mockSet.DbLiveInternalDeployer.DeployAsync(Arg.Any<ProjectSnapshot>(), DeployParameters.Default);
		});
	}

	[Fact]
	public async Task Project_is_loaded_first_and_deployed_after_the_self_deployment()
	{
		MockSet mockSet = new();
		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [] };
		mockSet.ProjectLoader.LoadAsync().Returns(project);

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		await dbLive.DeployAsync(DeployParameters.Default);

		Received.InOrder(() =>
		{
			mockSet.ProjectLoader.LoadAsync();
			mockSet.DbLiveDA.CheckDatabaseExistsAsync();
			mockSet.ProjectIdValidator.ValidateAsync();
			mockSet.DbLiveSelfDeployer.DeployAsync();
			mockSet.ProjectIdValidator.BindAsync();
			mockSet.DbLiveInternalDeployer.DeployAsync(project, DeployParameters.Default);
		});
	}

	[Theory]
	[MemberData(nameof(AllDeployParameters))]
	public async Task Error_of_the_project_stops_the_deployment_before_the_server_is_touched(DeployParameters parameters)
	{
		// an undo script left in the project after the upgrade to 2.0.
		MockSet mockSet = new();
		mockSet.ProjectLoader.LoadAsync()
			.Returns(Task.FromException<ProjectSnapshot>(new UndoNotSupportedException("Migrations/001.undo.sql")));

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		_ = await Assert.ThrowsAsync<UndoNotSupportedException>(() => dbLive.DeployAsync(parameters));

		// no database is created or recreated, and the self-migrations do not run: an older DbLive can still deploy the database.
		Assert.Empty(mockSet.DbLiveDA.ReceivedCalls());
		Assert.Empty(mockSet.ProjectIdValidator.ReceivedCalls());
		Assert.Empty(mockSet.DbLiveSelfDeployer.ReceivedCalls());
		Assert.Empty(mockSet.DbLiveInternalDeployer.ReceivedCalls());
	}

	public static TheoryData<DeployParameters> AllDeployParameters() =>
	[
		DeployParameters.Default,
		DeployParameters.Breaking,
		DeployParameters.Default with { CreateDbIfNotExists = true },
		DeployParameters.Default with { RecreateDatabase = true }
	];

	[Fact]
	public async Task Database_of_another_project_stops_the_deployment_before_the_self_deployment()
	{
		MockSet mockSet = new();
		mockSet.ProjectIdValidator.ValidateAsync()
			.Returns(Task.FromException(new ProjectIdMismatchException("ProjectId mismatch.")));

		DbLive dbLive = mockSet.CreateUsingMocks<DbLive>();

		_ = await Assert.ThrowsAsync<ProjectIdMismatchException>(() => dbLive.DeployAsync(DeployParameters.Default));

		// the self-migrations would change the dblive tables of the other project.
		await mockSet.DbLiveSelfDeployer.DidNotReceive().DeployAsync();
		await mockSet.ProjectIdValidator.DidNotReceive().BindAsync();
		await mockSet.DbLiveInternalDeployer.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!);
	}

	[Fact]
	public void Database_is_not_created_by_default()
	{
		// a typo in the database name of a production connection string must fail, not create an empty database.
		Assert.False(new DeployParameters().CreateDbIfNotExists);
		Assert.False(DeployParameters.Default.CreateDbIfNotExists);
		Assert.False(DeployParameters.Breaking.CreateDbIfNotExists);
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
		await mockSet.DbLiveInternalDeployer.Received().DeployAsync(Arg.Any<ProjectSnapshot>(), Arg.Is(parameters));
	}
}