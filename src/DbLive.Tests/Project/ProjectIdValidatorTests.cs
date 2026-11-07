namespace DbLive.Tests.Project;

public class ProjectIdValidatorTests
{
	[Fact]
	public async Task ValidateAsync_skips_when_project_id_not_set()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
		mockSet.Logger.Received(1).Warning(Arg.Is<string>(m => m.StartsWith("ProjectId is not set in settings.json")));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ValidateAsync_skips_when_project_id_is_blank(string? projectId)
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = projectId
		});

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
		mockSet.Logger.Received(1).Warning(Arg.Is<string>(m => m.StartsWith("ProjectId is not set in settings.json")));
	}

	[Fact]
	public async Task ValidateAsync_throws_when_project_id_exceeds_max_length()
	{
		MockSet mockSet = new();
		string longProjectId = new('a', ProjectIdValidator.MaxProjectIdLength + 1);
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = longProjectId
		});

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.ValidateAsync
		);

		Assert.Contains("128", ex.Message);
		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
	}

	[Fact]
	public async Task ValidateAsync_passes_a_database_without_the_DbLive_tables()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(false);

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateAsync_does_not_bind_the_project_id_when_database_has_none()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetProjectIdAsync().Returns((string?)null);

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		// it runs before the self-deployment, which may add the column: BindAsync stores the value after it.
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
		mockSet.Logger.DidNotReceive().Warning(Arg.Any<string>());
	}

	[Theory]
	[InlineData("my-app-prod")]
	[InlineData("My-App-Prod")]
	public async Task ValidateAsync_succeeds_when_project_id_matches_database_ignoring_case(string settingsProjectId)
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = settingsProjectId
		});
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("my-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateAsync_throws_when_project_id_mismatch()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("other-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.ValidateAsync
		);

		Assert.Equal(
			"ProjectId mismatch. Settings ProjectId: my-app-prod, database ProjectId: other-app-prod. Deployment blocked to prevent deploying to the wrong database.",
			ex.Message
		);
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task BindAsync_skips_when_project_id_not_set()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.BindAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
		// ValidateAsync warns once per deployment.
		mockSet.Logger.DidNotReceive().Warning(Arg.Any<string>());
	}

	[Theory]
	[InlineData("my-app-prod")]
	[InlineData("My-App-Prod")]
	public async Task BindAsync_persists_the_normalized_project_id_when_database_has_none(string settingsProjectId)
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = settingsProjectId
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns((string?)null);

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.BindAsync();

		await mockSet.DbLiveDA.Received(1).SetProjectIdAsync("my-app-prod");
		mockSet.Logger.Received(1).Information(
			"ProjectId bound to database: {ProjectId}.",
			"my-app-prod"
		);
	}

	[Fact]
	public async Task BindAsync_keeps_a_matching_project_id()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "My-App-Prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("my-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.BindAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task BindAsync_throws_when_project_id_mismatch()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("other-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.BindAsync
		);

		Assert.Contains("Deployment blocked", ex.Message);
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateBeforeTestsAsync_skips_when_project_id_not_set()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings());

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateBeforeTestsAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().GetProjectIdAsync();
		// the deployment warns; the tests run against a database the project was deployed to.
		mockSet.Logger.DidNotReceive().Warning(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateBeforeTestsAsync_throws_when_database_has_no_project_id()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns((string?)null);

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.ValidateBeforeTestsAsync
		);

		Assert.Equal(
			"The database has no ProjectId, settings ProjectId: my-app-prod. The database was not deployed by this project. Tests blocked to prevent running them against the wrong database.",
			ex.Message
		);
		// the tests only compare: binding the ProjectId is the job of a deployment.
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateBeforeTestsAsync_succeeds_when_project_id_matches_database()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "My-App-Prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("my-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateBeforeTestsAsync();

		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task ValidateBeforeTestsAsync_throws_when_project_id_mismatch()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("other-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.ValidateBeforeTestsAsync
		);

		Assert.Equal(
			"ProjectId mismatch. Settings ProjectId: my-app-prod, database ProjectId: other-app-prod. Tests blocked to prevent running them against the wrong database.",
			ex.Message
		);
	}
}
