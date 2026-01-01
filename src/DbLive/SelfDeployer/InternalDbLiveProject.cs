

namespace DbLive.SelfDeployer;

internal class InternalDbLiveProject(
		IDbLivePaths projectPath,
		IFileSystem _fileSystem		
	) : IInternalDbLiveProject
{
	private readonly string _projectPath = projectPath.GetPathToDbLiveSelfProject();

	public IReadOnlyList<Migration> GetMigrations()
	{
		string migrationsPath = _projectPath;

		IEnumerable<string> migrationFiles = _fileSystem.EnumerateFiles(migrationsPath, "*.sql", subfolders: true);

		List<Migration> migrations = [];

		foreach (string filePath in migrationFiles)
		{
			MigrationItemInfo info = MigrationFileNameParser.GetMigrationInfo(filePath);
			
			MigrationItem migrationItem = new()
			{
				MigrationItemType = info.MigrationItemType,
				Name = info.Name,
				FileData = _fileSystem.ReadFileData(info.FilePath, _projectPath)
			};

			Migration migration = new()
			{
				Version = info.Version,
				Items = new Dictionary<MigrationItemType, MigrationItem>
				{
					{ info.MigrationItemType, migrationItem }
				}
			};

			migrations.Add(migration);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}
}
