namespace EasyFlow.Project;

public class EasyFlowProject : IEasyFlowProject
{
	private readonly IFileSystem _fileSystem;

	private EasyFlowSettings _settings = new();
	private string _projectPath = "";
	private bool _isLoaded = false;

	public EasyFlowProject(IFileSystem fileSystem)
	{
		_fileSystem = fileSystem;
	}

	public void Load(string projectPath)
	{
		_isLoaded = true;
		_projectPath = projectPath;
		string settingsPath = Path.Combine(projectPath, "settings.json");
		if (_fileSystem.FileExists(settingsPath))
		{
			string settingsJson = _fileSystem.FileReadAllText(settingsPath);
			_settings = JsonConvert.DeserializeObject<EasyFlowSettings>(settingsJson) ?? _settings;
		}
	}

	public EasyFlowSettings GetSettings()
	{
		return _settings;
	}

	public HashSet<MigrationTask> GetMigrationTasks(string migrationFolder)
	{
		ThrowIfProjectWasNotLoaded();
		HashSet<MigrationTask> tasks = new();

		var files = _fileSystem.EnumerateFiles(migrationFolder, "*.sql", _settings.TestFilePattern, true);

		foreach (string filePath in files)
		{
			string fileName = filePath.GetLastSegment();
			var fileParts = fileName.Split(".");

			var migrationType = GetMigrationType(fileParts[0]);

			MigrationTask task = new()
			{
				MigrationType = migrationType,
				FilePath = filePath
			};

			if (tasks.Contains(task))
			{
				throw new MigrationTaskExistsException(task);
			}

			tasks.Add(task);
		}

		return tasks;
	}

	private void ThrowIfProjectWasNotLoaded()
	{
		if (_isLoaded == false) throw new ProjectWasNotLoadedException();
	}

	public static MigrationType GetMigrationType(string type) =>
		type.ToLower() switch
		{
			"migration" => MigrationType.Migration,
			"undo" => MigrationType.Undo,
			"data" => MigrationType.Data,
			"breaking" => MigrationType.BreakingChange,
			_ => throw new UnknowMigrationTaskTypeException(type)
		};

	public IEnumerable<CodeItem> GetCodeItems()
	{
		ThrowIfProjectWasNotLoaded();
		List<CodeItem> codeItems = new();
		string codePath = Path.Combine(_projectPath, "Code");
		if (Path.Exists(codePath))
		{
			var files = _fileSystem.EnumerateFiles(codePath, "*.sql", _settings.TestFilePattern, true);
			foreach (string filePath in files)
			{
				string fileName = filePath.GetLastSegment();
				var codeItem = new CodeItem { Name = fileName, FilePath = filePath };
				codeItems.Add(codeItem);
			}
		}
		return codeItems;
	}

	public IEnumerable<Migration> GetMigrations()
	{
		ThrowIfProjectWasNotLoaded();
		HashSet<Migration> migrations = new();
		
		string migrationsPath = _projectPath.CombineWith("Migrations");
		string oldMigrationsPath = migrationsPath.CombineWith("_Old");

		var migrationDirectories = _fileSystem.EnumerateDirectories(new[] { migrationsPath, oldMigrationsPath }, "*.*", SearchOption.TopDirectoryOnly);

		foreach (string folderPath in migrationDirectories)
		{
			string folderName = folderPath.GetLastSegment();

			if (folderName == "_Old") continue;

			var migration = ReadMigration(folderPath, folderName);

			if (migrations.Contains(migration))
			{
				throw new MigrationExistsException(migration);
			}

			migrations.Add(migration);
		}
		return migrations.OrderBy(m => m.Version).ThenBy(m => m.Name);
	}

	private Migration ReadMigration(string folderPath, string folderName)
	{
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
			Path = folderPath,
			Tasks = GetMigrationTasks(folderPath)
		};
	}
}
