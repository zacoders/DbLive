namespace DbLive.Project;

internal class ProjectLoader(
		IDbLiveProject _project,
		ISettingsAccessor _settingsAccessor,
		ITransactionSettingsValidator _transactionSettingsValidator,
		IDbLiveDA _da
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

		// the project does not know the provider: the isolation levels of the settings files of the migrations are checked here.
		_transactionSettingsValidator.ValidateMigrations(migrations);

		ProjectSnapshot project = new()
		{
			Migrations = migrations,
			CodeGroups = await _project.GetCodeGroupsAsync().ConfigureAwait(false),
			CodeBreakingItems = await _project.GetCodeBreakingItemsAsync().ConfigureAwait(false),
			BeforeDeployItems = await _project.GetFolderItemsAsync(ProjectFolder.BeforeDeploy).ConfigureAwait(false),
			AfterDeployItems = await _project.GetFolderItemsAsync(ProjectFolder.AfterDeploy).ConfigureAwait(false)
		};

		// the same for the paths that the dblive tables of the provider store as a key. The paths of migrations are not a key, and
		// the tests write nothing.
		ThrowIfPathTooLong(
			[.. project.CodeGroups.SelectMany(g => g.CodeItems).Select(i => i.FileData), .. project.CodeBreakingItems.Select(i => i.FileData)],
			_da.MaxCodePathLength,
			"a code file or a breaking script of the Code folder"
		);

		ThrowIfPathTooLong(
			[.. project.BeforeDeployItems.Select(i => i.FileData), .. project.AfterDeployItems.Select(i => i.FileData)],
			_da.MaxFolderItemPathLength,
			"a BeforeDeploy or AfterDeploy file"
		);

		return project;
	}

	/// <exception cref="ScriptPathTooLongException">The path of one of the files is longer than <paramref name="maxLength"/>.</exception>
	private static void ThrowIfPathTooLong(IEnumerable<FileData> files, int? maxLength, string fileKind)
	{
		// the stored path is in lower case (ToStoredPath), of the same length: .NET maps each UTF-16 character to one.
		string? tooLong = maxLength is null ? null : files.Select(f => f.RelativePath).FirstOrDefault(p => p.Length > maxLength);

		if (tooLong is not null)
		{
			throw new ScriptPathTooLongException(tooLong, maxLength!.Value, fileKind);
		}
	}
}
