namespace DbLive.Project;

/// <summary>
/// The project a deployment deploys, read and checked by <see cref="IProjectLoader"/> before anything touches the server.
/// The phases of the deployment take it as an argument and do not read the project themselves.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ProjectSnapshot
{
	/// <inheritdoc cref="IDbLiveProject.GetMigrationsAsync"/>
	public required IReadOnlyList<Migration> Migrations { get; init; }

	/// <inheritdoc cref="IDbLiveProject.GetCodeGroupsAsync"/>
	public required IReadOnlyList<CodeGroup> CodeGroups { get; init; }

	/// <inheritdoc cref="IDbLiveProject.GetCodeBreakingItemsAsync"/>
	public required IReadOnlyList<GenericItem> CodeBreakingItems { get; init; }

	/// <summary>
	/// The scripts of the BeforeDeploy folder, sorted by relative path, ordinal, ignoring case.
	/// </summary>
	public required IReadOnlyList<GenericItem> BeforeDeployItems { get; init; }

	/// <summary>
	/// The scripts of the AfterDeploy folder, sorted by relative path, ordinal, ignoring case.
	/// </summary>
	public required IReadOnlyList<GenericItem> AfterDeployItems { get; init; }
}
