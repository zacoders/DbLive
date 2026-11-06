namespace DbLive.Deployers.Migrations;

/// <summary>
/// The database has applied migration versions that the project does not have: an older build is deployed,
/// or migration files were deleted from the project. The deployment stops before it changes anything.
/// </summary>
[ExcludeFromCodeCoverage]
public class DatabaseAheadOfProjectException(IReadOnlyList<long> versions)
	: Exception(
		$"The database has applied migration versions that the project does not have: {string.Join(", ", versions)}. "
		+ "Either an older build is deployed: DbLive does not roll back a database, so roll back the application and keep the database, "
		+ "which serves the previous application until the breaking phase of the release runs (see \"Roll back a release\" in the DbLive docs). "
		+ "Or migration files were deleted from the project: restore them."
	)
{
	/// <summary>
	/// The applied versions that the project does not have, in ascending order.
	/// </summary>
	public IReadOnlyList<long> Versions { get; private set; } = versions;
}
