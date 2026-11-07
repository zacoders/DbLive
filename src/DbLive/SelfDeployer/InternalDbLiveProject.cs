
namespace DbLive.SelfDeployer;

internal class InternalDbLiveProject(
		IInternalScriptSource _scriptSource
	) : IInternalDbLiveProject
{
	public async Task<IReadOnlyList<InternalMigration>> GetMigrationsAsync()
	{
		IReadOnlyList<string> migrationFiles = _scriptSource.GetFiles();

		// otherwise the self-deployer deploys nothing, and the deployment fails later without the dblive schema.
		if (migrationFiles.Count == 0)
		{
			throw new InternalException($"DbLive's own scripts are not embedded in {_scriptSource.Location}.");
		}

		List<InternalMigration> migrations = [];

		foreach (string relativePath in migrationFiles)
		{
			MigrationItemInfo info = MigrationFileNameParser.GetMigrationInfo(relativePath);

			InternalMigration migrationItem = new()
			{
				Version = info.Version,
				Name = info.Name,
				FileData = await _scriptSource.ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};

			migrations.Add(migrationItem);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}
}
