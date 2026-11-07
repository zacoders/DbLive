namespace DbLive.Deployers.Migrations;

/// <summary>
/// Stops a deployment whose project does not have every migration version applied to the database: an older build
/// (DbLive does not roll back a database), or migration files deleted from the project.
/// </summary>
internal class AppliedMigrationsValidator(
	IDbLiveProject project,
	IDbLiveDA da
) : IAppliedMigrationsValidator
{
	public async Task ValidateAsync()
	{
		HashSet<long> projectVersions = [.. (await project.GetMigrationsAsync().ConfigureAwait(false)).Select(m => m.Version)];

		IReadOnlyCollection<long> appliedVersions = await da.GetAppliedMigrationVersionsAsync().ConfigureAwait(false);

		List<long> missingVersions = [.. appliedVersions.Where(v => !projectVersions.Contains(v)).Order()];

		if (missingVersions.Count > 0)
		{
			throw new DatabaseAheadOfProjectException(missingVersions);
		}
	}
}
