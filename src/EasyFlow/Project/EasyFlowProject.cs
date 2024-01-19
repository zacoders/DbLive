namespace EasyFlow.Project;

public class EasyFlowProject : IEasyFlowProject
{
	private readonly EasyFlowSettings _settings = new();
	private readonly string _projectPath;
	private readonly IFileSystem _fileSystem;

	public EasyFlowProject(IEasyFlowProjectPath projectPath, IFileSystem fileSystem)
	{
		_projectPath = projectPath.ProjectPath;
		_fileSystem = fileSystem;

		if (!fileSystem.PathExists(projectPath.ProjectPath)
			 && fileSystem.IsDirectoryEmpty(projectPath.ProjectPath))
		{
			throw new ProjectFolderIsEmptyException(projectPath.ProjectPath);
		}

		string settingsPath = Path.Combine(_projectPath, "settings.json");
		if (_fileSystem.FileExists(settingsPath))
		{
			var settingsJson = _fileSystem.FileReadAllText(settingsPath);
			_settings = JsonConvert.DeserializeObject<EasyFlowSettings>(settingsJson) ?? _settings;
		}
	}

	public EasyFlowSettings GetSettings()
	{
		return _settings;
	}

	public ReadOnlyCollection<MigrationItem> GetMigrationItems(string migrationFolder)
	{
		List<MigrationItem> tasks = [];

		var files = _fileSystem.EnumerateFiles(migrationFolder, "*.sql", true);

		foreach (string filePath in files.OrderBy(path => path))
		{
			string fileName = filePath.GetLastSegment();
			var fileParts = fileName.Split('.');

			var migrationType = GetMigrationType(fileParts[0]);

			MigrationItem task = new()
			{
				MigrationItemType = migrationType,
				FileData = _fileSystem.ReadFileData(filePath, _projectPath)
			};

			tasks.Add(task);
		}

		return tasks.AsReadOnly();
	}

	public static MigrationItemType GetMigrationType(string type) =>
		type.ToLower() switch
		{
			"migration" => MigrationItemType.Migration,
			"m" => MigrationItemType.Migration,
			"undo" => MigrationItemType.Undo,
			"u" => MigrationItemType.Undo,
			"breaking" => MigrationItemType.BreakingChange,
			"b" => MigrationItemType.BreakingChange,
			_ => throw new UnknowMigrationItemTypeException(type)
		};

	public IEnumerable<CodeItem> GetCodeItems()
	{
		List<CodeItem> codeItems = [];
		string codePath = Path.Combine(_projectPath, "Code");
		if (_fileSystem.PathExists(codePath))
		{
			var files = _fileSystem.EnumerateFiles(codePath, ["*.sql"], _settings.TestFilePatterns, true);
			foreach (string filePath in files)
			{
				string fileName = filePath.GetLastSegment();
				var codeItem = new CodeItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
				codeItems.Add(codeItem);
			}
		}
		return codeItems;
	}

	public IEnumerable<Migration> GetMigrations()
	{
		HashSet<Migration> migrations = [];

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
		var splitFolder = folderName.Split('.');

		string migrationVersionStr = splitFolder[0];

		if (!int.TryParse(migrationVersionStr, out var version))
		{
			throw new MigrationVersionParseException(folderName, migrationVersionStr);
		}

		return new Migration
		{
			Version = version,
			Name = splitFolder[1],
			FolderPath = folderPath,
			Items = GetMigrationItems(folderPath)
		};
	}

	public IReadOnlyCollection<TestItem> GetTests()
	{
		List<TestItem> tests = [];

		string testsPath = _projectPath.CombineWith("Tests");
		string codePath = _projectPath.CombineWith("Code");

		if (!_fileSystem.PathExists(testsPath))
		{
			return Array.Empty<TestItem>();
		}

		var testFiles = _fileSystem.EnumerateFiles(testsPath, _settings.TestFilePatterns, subfolders: true)
			.Union(_fileSystem.EnumerateFiles(codePath, _settings.TestFilePatterns, subfolders: true));

		foreach (string testFilePath in testFiles)
		{
			TestItem testItem = new()
			{
				Name = testFilePath.GetLastSegment(),
				FileData = _fileSystem.ReadFileData(testFilePath, _projectPath)
			};

			tests.Add(testItem);
		}

		return tests;
	}

	/// <inheritdoc/>
	public ReadOnlyCollection<GenericItem> GetFolderItems(ProjectFolder projectFolder)
	{
		Dictionary<string, GenericItem> items = [];

		string folderPath = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => _settings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => _settings.AfterDeployFolder,
			_ => throw new NotImplementedException($"Unknown project folder {projectFolder}")
		};

		string fullPath = Path.Combine(_projectPath, folderPath);

		if (_fileSystem.PathExists(fullPath))
		{
			var files = _fileSystem.EnumerateFiles(fullPath, "*.sql", true);
			foreach (string filePath in files)
			{
				string fileName = filePath.GetLastSegment();
				var item = new GenericItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
				items.Add(filePath, item);
			}
		}

		// sorting the items by full path which is in the key of the dictionary.
		return items.OrderBy(i => i.Key).Select(i => i.Value).ToList().AsReadOnly();
	}
}
