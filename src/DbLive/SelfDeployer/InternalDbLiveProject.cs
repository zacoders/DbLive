
namespace DbLive.SelfDeployer;

internal class InternalDbLiveProject(
		IInternalProjectPath _projectPath,
		IFileSystem _fileSystem
	) : IInternalDbLiveProject
{
	public async Task<IReadOnlyList<InternalMigration>> GetMigrationsAsync()
	{
		IEnumerable<string> migrationFiles = _fileSystem.EnumerateFiles(_projectPath.Path, "*.sql", subfolders: true);

		List<InternalMigration> migrations = [];

		foreach (string filePath in migrationFiles)
		{
			MigrationItemInfo info = MigrationFileNameParser.GetMigrationInfo(filePath);

			InternalMigration migrationItem = new()
			{
				Version = info.Version,				
				Name = info.Name,
				FileData = await _fileSystem.ReadFileDataAsync(info.FilePath, _projectPath.Path)
			};

			migrations.Add(migrationItem);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}
}
