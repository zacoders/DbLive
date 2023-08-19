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
			_settings = JsonConvert.DeserializeObject<EasyFlowSettings>(settingsJson) ?? new EasyFlowSettings();
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
			"testdata" => MigrationType.TestData,
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
			foreach (string filePath in _fileSystem.EnumerateFiles(codePath, "*.sql", true))
			{
				var fileUri = new Uri(filePath);
				string fileName = fileUri.GetLastSegment();
				var codeItem = new CodeItem { Name = fileName, FileUri = fileUri };
				codeItems.Add(codeItem);
			}
		}
		return codeItems;
	}

	public IEnumerable<Migration> GetMigrations()
	{
		ThrowIfProjectWasNotLoaded();
		HashSet<Migration> migrations = new();
		string migrationsPath = Path.Combine(_projectPath, "Migrations");
		foreach (string folderPath in _fileSystem.EnumerateDirectories(migrationsPath, "*.*", SearchOption.AllDirectories))
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
