namespace EasyFlow.Project;

public class EasyFlowProject : IEasyFlowProject
{
	private readonly string _projectPath;
	private readonly IFileSystem _fileSystem;
	private readonly ISettingsAccessor _settingsAccessor;

	public EasyFlowProject(IEasyFlowProjectPath projectPath, IFileSystem fileSystem, ISettingsAccessor settingsAccessor)
	{
		_projectPath = projectPath.ProjectPath;
		_fileSystem = fileSystem;
		_settingsAccessor = settingsAccessor;


		if (!fileSystem.PathExistsAndNotEmpty(projectPath.ProjectPath))
		{
			throw new ProjectFolderIsEmptyException(projectPath.ProjectPath);
		}
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

	//public IEnumerable<CodeGroup> GetCodeGroups2()
	//{
	//	string codePath = _projectPath.CombineWith(_settings.CodeFolder);

	//	var paths = _settings.CodeSubFoldersDeploymentOrder.Select(codePath.CombineWith).ToList();

	//	foreach (var path in paths) {
	//		yield return new CodeGroup { Path = path, CodeItems = GetCodeItems(path) };
	//	}

	//	//var allCodeDirs = _fileSystem.EnumerateDirectories(codePath, "*", SearchOption.AllDirectories)
	//	//	.Except(pa);
	//}

	public IEnumerable<CodeGroup> GetCodeGroups()
	{
		var settings = _settingsAccessor.ProjectSettings;
		string codePath = _projectPath.CombineWith(settings.CodeFolder);

		var subPaths = settings.CodeSubFoldersDeploymentOrder.Select(codePath.CombineWith).ToList();

		List<string> codeFiles = _fileSystem.EnumerateFiles(codePath, ["*.sql"], settings.TestFilePatterns, true).ToList();

		foreach (string subPath in subPaths)
		{
			var files = codeFiles.RemoveWhere(f => f.ToLower().StartsWith(subPath.ToLower() + Path.DirectorySeparatorChar));
			yield return new CodeGroup
			{
				Path = codePath,
				CodeItems = GetCodeGroup(files)
			};
		}

		// everething else as a separate group.
		yield return new CodeGroup
		{
			Path = codePath,
			CodeItems = GetCodeGroup(codeFiles)
		};
	}

	internal List<CodeItem> GetCodeGroup(List<string> codeFiles)
	{
		List<CodeItem> codeItems = [];
		
		foreach (string filePath in codeFiles)
		{
			string fileName = filePath.GetLastSegment();
			var codeItem = new CodeItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	//internal IReadOnlyCollection<CodeItem> GetCodeItems(string codePath)
	//{
	//	List<CodeItem> codeItems = [];

	//	if (!_fileSystem.PathExists(codePath))
	//	{
	//		return codeItems;
	//	}

	//	var files = _fileSystem.EnumerateFiles(codePath, ["*.sql"], _settings.TestFilePatterns, true);
	//	foreach (string filePath in files)
	//	{
	//		string fileName = filePath.GetLastSegment();
	//		var codeItem = new CodeItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
	//		codeItems.Add(codeItem);
	//	}

	//	return codeItems;
	//}

	//public IEnumerable<CodeItem> GetCodeItems()
	//{
	//	List<CodeItem> codeItems = [];
	//	string codePath = Path.Combine(_projectPath, _settings.CodeFolder);
	//	if (_fileSystem.PathExists(codePath))
	//	{
	//		var files = _fileSystem.EnumerateFiles(codePath, ["*.sql"], _settings.TestFilePatterns, true);
	//		foreach (string filePath in files)
	//		{
	//			string fileName = filePath.GetLastSegment();
	//			var codeItem = new CodeItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
	//			codeItems.Add(codeItem);
	//		}
	//	}
	//	return codeItems;
	//}

	public IEnumerable<Migration> GetMigrations()
	{
		HashSet<Migration> migrations = [];

		string migrationsPath = _projectPath.CombineWith("Migrations");
		string oldMigrationsPath = migrationsPath.CombineWith("_Old");

		var migrationDirectories = _fileSystem.EnumerateDirectories(new[] { migrationsPath, oldMigrationsPath }, "*", SearchOption.TopDirectoryOnly);

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
		var settings = _settingsAccessor.ProjectSettings;

		string testsPath = _projectPath.CombineWith(settings.TestsFolder);
		string codePath = _projectPath.CombineWith(settings.CodeFolder);

		var testFolders = _fileSystem.EnumerateDirectories([codePath, testsPath], "*", SearchOption.AllDirectories);

		List<TestItem> testGroups = [];

		foreach (var folderPath in testFolders)
		{
			testGroups.AddRange(GetFolderTests(folderPath));
		}

		return testGroups.AsReadOnly();
	}

	internal List<TestItem> GetFolderTests(string folderPath)
	{
		List<TestItem> tests = [];

		FileData? initFileData = null;
		string initializeFilePath = folderPath.CombineWith("init.sql");
		if (_fileSystem.FileExists(initializeFilePath))
		{
			initFileData = _fileSystem.ReadFileData(initializeFilePath, _projectPath);
		}

		var testFiles = _fileSystem.EnumerateFiles(folderPath, _settingsAccessor.ProjectSettings.TestFilePatterns, subfolders: false);

		foreach (string testFilePath in testFiles)
		{
			TestItem testItem = new()
			{
				Name = testFilePath.GetLastSegment(),
				FileData = _fileSystem.ReadFileData(testFilePath, _projectPath),
				InitFileData = initFileData,
				Folder = folderPath
			};

			tests.Add(testItem);
		}

		return tests;
	}

	/// <inheritdoc/>
	public ReadOnlyCollection<GenericItem> GetFolderItems(ProjectFolder projectFolder)
	{
		Dictionary<string, GenericItem> items = [];

		var settings = _settingsAccessor.ProjectSettings;

		string folderPath = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => settings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => settings.AfterDeployFolder,
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
