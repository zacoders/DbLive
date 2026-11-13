namespace DbLive.Tests.Deployers.Migrations;

public class AppliedMigrationsValidatorTests
{
	[Fact]
	public async Task Passes_when_the_project_has_every_applied_version()
	{
		MockSet mockSet = new();
		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1), NewMigration(2), NewMigration(3)] };
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns([1, 2]);

		AppliedMigrationsValidator validator = mockSet.CreateUsingMocks<AppliedMigrationsValidator>();

		await validator.ValidateAsync(project);
	}

	[Fact]
	public async Task Passes_for_a_new_database()
	{
		MockSet mockSet = new();
		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1)] };
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns([]);

		AppliedMigrationsValidator validator = mockSet.CreateUsingMocks<AppliedMigrationsValidator>();

		await validator.ValidateAsync(project);
	}

	[Fact]
	public async Task Older_build_fails_and_names_the_versions_it_does_not_have()
	{
		MockSet mockSet = new();
		// the database was deployed by a release with versions 3 and 20260105120000, the project is the release before it.
		ProjectSnapshot project = ProjectSnapshots.Empty with { Migrations = [NewMigration(1), NewMigration(2)] };
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns([20260105120000, 1, 3, 2]);

		AppliedMigrationsValidator validator = mockSet.CreateUsingMocks<AppliedMigrationsValidator>();

		DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(() => validator.ValidateAsync(project));

		Assert.Equal([3, 20260105120000], ex.Versions);
		Assert.StartsWith("The database has applied migration versions that the project does not have: 3, 20260105120000. ", ex.Message);
		// both causes and what to do.
		Assert.Contains("an older build is deployed: DbLive does not roll back a database, so roll back the application and keep the database", ex.Message);
		Assert.Contains("Roll back a release", ex.Message);
		Assert.Contains("migration files were deleted from the project: restore them", ex.Message);
	}

	[Fact]
	public async Task Project_without_migrations_fails_on_a_database_with_applied_versions()
	{
		MockSet mockSet = new();
		ProjectSnapshot project = ProjectSnapshots.Empty;
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns([1]);

		AppliedMigrationsValidator validator = mockSet.CreateUsingMocks<AppliedMigrationsValidator>();

		DatabaseAheadOfProjectException ex = await Assert.ThrowsAsync<DatabaseAheadOfProjectException>(() => validator.ValidateAsync(project));

		Assert.Equal([1], ex.Versions);
	}

	private static Migration NewMigration(long version) => new()
	{
		Version = version,
		Items = new Dictionary<MigrationItemType, MigrationItem>
		{
			[MigrationItemType.Migration] = new()
			{
				MigrationItemType = MigrationItemType.Migration,
				FileData = new FileData
				{
					Content = "-- migration",
					RelativePath = $"Migrations/{version:D3}.migration.sql"
				}
			}
		}
	};
}
