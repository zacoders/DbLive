
namespace DbLive.Project;

public class DbLiveProject(
	IProjectPathAccessor projectPath,
	IFileSystem _fileSystem,
	ISettingsAccessor _settingsAccessor
) : IDbLiveProject
{
	private readonly string _projectPath = projectPath.ProjectPath;

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
				Path = subPath,
				CodeItems = GetCodeGroupItems(files)
			};
		}

		// everything else as a separate group.
		yield return new CodeGroup
		{
			Path = codePath,
			CodeItems = GetCodeGroupItems(codeFiles)
		};
	}

	internal List<CodeItem> GetCodeGroupItems(List<string> codeFiles)
	{
		List<CodeItem> codeItems = [];

		foreach (string filePath in codeFiles)
		{
			string fileName = Path.GetFileName(filePath);
			var codeItem = new CodeItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	public IReadOnlyList<Migration> GetMigrations()
	{
		var settings = _settingsAccessor.ProjectSettings;
		string migrationsPath = _projectPath.CombineWith(settings.MigrationsFolder);

		IEnumerable<string> migrationFiles = _fileSystem.EnumerateFiles(migrationsPath, ["*.sql", "*.json"], subfolders: true);

		List<MigrationItemInfo> migrationItems = [];

		foreach (string filePath in migrationFiles)
		{
			MigrationItemInfo migrationItemInfo = MigrationFileNameParser.GetMigrationInfo(filePath);
			migrationItems.Add(migrationItemInfo);
		}

		List<Migration> migrations = [];
		foreach (IGrouping<int, MigrationItemInfo> migrationGroup in migrationItems.ToLookup(i => i.Version))
		{
			int migrationVersion = migrationGroup.Key;
			Dictionary<MigrationItemType, MigrationItem> itmes = [];
			foreach (var item in migrationGroup)
			{
				if (itmes.ContainsKey(item.MigrationItemType))
				{
					throw new DuplicateMigrationItemException(migrationVersion, item.MigrationItemType);
				}
				MigrationItem migrationItem = new()
				{
					MigrationItemType = item.MigrationItemType,
					Name = item.Name,
					FileData = _fileSystem.ReadFileData(item.FilePath, _projectPath)
				};
				itmes.Add(item.MigrationItemType, migrationItem);
			}

			Migration migration = new()
			{
				Version = migrationGroup.Key,
				Items = itmes
			};
			migrations.Add(migration);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}

	public IReadOnlyCollection<TestItem> GetTests()
	{
		var settings = _settingsAccessor.ProjectSettings;

		string testsPath = _projectPath.CombineWith(settings.TestsFolder);
		string codePath = _projectPath.CombineWith(settings.CodeFolder);

		var testFolders = _fileSystem.EnumerateDirectories([codePath, testsPath], "*", SearchOption.AllDirectories);

		List<TestItem> testGroups = [];

		foreach (var folderPath in testFolders.Union([codePath, testsPath]))
		{
			testGroups.AddRange(GetFolderTests(folderPath));
		}

		return testGroups.AsReadOnly();
	}

	internal List<TestItem> GetFolderTests(string folderPath)
	{
		if (!_fileSystem.PathExists(folderPath)) return [];

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
				Name = Path.GetFileName(testFilePath),
				FileData = _fileSystem.ReadFileData(testFilePath, _projectPath),
				InitFileData = initFileData
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
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
		};

		string fullPath = Path.Combine(_projectPath, folderPath);

		if (_fileSystem.PathExists(fullPath))
		{
			var files = _fileSystem.EnumerateFiles(fullPath, "*.sql", true);
			foreach (string filePath in files)
			{
				string fileName = Path.GetFileName(filePath);
				var item = new GenericItem { Name = fileName, FileData = _fileSystem.ReadFileData(filePath, _projectPath) };
				items.Add(filePath, item);
			}
		}

		// sorting the items by full path which is in the key of the dictionary.
		return items.OrderBy(i => i.Key).Select(i => i.Value).ToList().AsReadOnly();
	}

	[ExcludeFromCodeCoverage]
	public string GetVisualStudioProjectPath()
	{
		return projectPath.VisualStudioProjectPath;
	}
}
