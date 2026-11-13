namespace DbLive.Project;

internal class ProjectLoader(
		IDbLiveProject _project,
		ISettingsAccessor _settingsAccessor,
		ITransactionSettingsValidator _transactionSettingsValidator
	) : IProjectLoader
{
	/// <inheritdoc/>
	public async Task<ProjectSnapshot> LoadAsync()
	{
		DbLiveSettings projectSettings = await _settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		// needs only the provider, no connection: the database may not exist yet.
		await _transactionSettingsValidator.ValidateAsync(projectSettings).ConfigureAwait(false);

		// the first read checks all files of the project (IDbLiveProject keeps them, the other reads take them from there).
		IReadOnlyList<Migration> migrations = await _project.GetMigrationsAsync().ConfigureAwait(false);

		return new ProjectSnapshot
		{
			Migrations = migrations,
			CodeGroups = await _project.GetCodeGroupsAsync().ConfigureAwait(false),
			CodeBreakingItems = await _project.GetCodeBreakingItemsAsync().ConfigureAwait(false),
			BeforeDeployItems = await _project.GetFolderItemsAsync(ProjectFolder.BeforeDeploy).ConfigureAwait(false),
			AfterDeployItems = await _project.GetFolderItemsAsync(ProjectFolder.AfterDeploy).ConfigureAwait(false)
		};
	}
}
