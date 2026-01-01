
namespace DbLive.SelfDeployer;

internal class InternalDbLiveProject(
		IInternalProjectPath _projectPath,
		IFileSystem _fileSystem
	) : IInternalDbLiveProject
{
	public IReadOnlyList<InternalMigration> GetMigrations()
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
				FileData = _fileSystem.ReadFileData(info.FilePath, _projectPath.Path)
			};

			migrations.Add(migrationItem);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}
}
