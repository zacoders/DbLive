namespace EasyFlow;

public class EasyFlowProject : IEasyFlowProject
{
	private readonly IFileSystem _fileSystem;

	public EasyFlowProject(IFileSystem fileSystem)
	{
		_fileSystem = fileSystem;
	}

	public HashSet<MigrationTask> GetMigrationTasks(string migrationFolder)
	{
		HashSet<MigrationTask> tasks = new();
		foreach (string file in _fileSystem.EnumerateFiles(migrationFolder, "*.sql"))
		{
			var fileUri = new Uri(file);
			string fileName = fileUri.GetLastSegment();
			var fileParts = fileName.Split(".");

			var migrationType = GetMigrationType(fileParts[0]);

			MigrationTask task = new()
			{
				MigrationType = migrationType,
				FileUri = fileUri
			};

			if (tasks.Contains(task))
			{
				throw new MigrationTaskExistsException(task);
			}

			tasks.Add(task);
		}
		return tasks;
	}

	public static MigrationType GetMigrationType(string type) =>
		type.ToLower() switch
		{
			"migration" => MigrationType.Migration,
			"undo" => MigrationType.Undo,
			"data" => MigrationType.Data,
			"testdata" => MigrationType.TestData,
			"breaking" => MigrationType.BreakingChange,
			_ => throw new UnknowMigrationTaskTypeException(type)
		};

	public IEnumerable<Migration> GetProjectMigrations(string projectPath)
	{
		HashSet<Migration> migrations = new();
		foreach (string folderPath in _fileSystem.EnumerateDirectories(projectPath, "*.*", SearchOption.AllDirectories))
		{
			Uri folderUri = new(folderPath);
			string folderName = folderUri.GetLastSegment();

			if (folderName == "_Old") continue;

			var migration = ReadMigration(folderUri);

			if (migrations.Contains(migration))
			{
				throw new MigrationExistsException(migration);
			}

			migrations.Add(migration);
		}
		return migrations.OrderBy(m => m.Version).ThenBy(m => m.Name);
	}

	private Migration ReadMigration(Uri folderUri)
	{
		string folderName = folderUri.GetLastSegment();
		var splitFolder = folderName.Split(".");

		string migrationVersionStr = splitFolder[0];

		if (!int.TryParse(migrationVersionStr, out var version))
		{
			throw new MigrationVersionParseException(folderName, migrationVersionStr);
		}

		return new Migration
		{
			Version = version,
			Name = splitFolder[1],
			PathUri = folderUri,
			Tasks = GetMigrationTasks(folderUri.LocalPath)
		};
	}
}
