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
	public async Task ValidateAsync_persists_project_id_when_database_has_none()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
		mockSet.DbLiveDA.GetProjectIdAsync().Returns((string?)null);

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		await validator.ValidateAsync();

		await mockSet.DbLiveDA.Received(1).SetProjectIdAsync("my-app-prod");
		mockSet.Logger.Received(1).Information(
			"ProjectId bound to database: {ProjectId}.",
			"my-app-prod"
		);
	}

	[Fact]
	public async Task ValidateAsync_succeeds_when_project_id_matches_database()
	{
		MockSet mockSet = new();
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			ProjectId = "my-app-prod"
		});
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
		mockSet.DbLiveDA.GetProjectIdAsync().Returns("other-app-prod");

		ProjectIdValidator validator = mockSet.CreateUsingMocks<ProjectIdValidator>();

		ProjectIdMismatchException ex = await Assert.ThrowsAsync<ProjectIdMismatchException>(
			validator.ValidateAsync
		);

		Assert.Contains("my-app-prod", ex.Message);
		Assert.Contains("other-app-prod", ex.Message);
		Assert.Contains("Deployment blocked", ex.Message);
		_ = mockSet.DbLiveDA.DidNotReceive().SetProjectIdAsync(Arg.Any<string>());
	}
}
