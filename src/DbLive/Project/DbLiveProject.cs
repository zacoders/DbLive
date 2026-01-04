
namespace DbLive.Project;

public class DbLiveProject(
	IProjectPath projectPath,
	IFileSystem _fileSystem,
	ISettingsAccessor settingsAccessor,
	IVsProjectPathAccessor vsProjectPathAccessor
) : IDbLiveProject
{
	private readonly string _projectPath = projectPath.Path;
	private readonly DbLiveSettings _projectSettings = settingsAccessor.ProjectSettings;

	public IEnumerable<CodeGroup> GetCodeGroups()
	{
		string codePath = _projectPath.CombineWith(_projectSettings.CodeFolder);

		var subPaths = _projectSettings.CodeSubFoldersDeploymentOrder.Select(codePath.CombineWith).ToList();

		List<string> codeFiles = [.. _fileSystem.EnumerateFiles(codePath, ["*.sql"], _projectSettings.TestFilePatterns, true)];

		foreach (string subPath in subPaths)
		{
			var files = codeFiles.RemoveWhere(f => f.StartsWith(subPath + Path.DirectorySeparatorChar, StringComparison.CurrentCultureIgnoreCase));
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
		string migrationsPath = _projectPath.CombineWith(_projectSettings.MigrationsFolder);

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
			Dictionary<MigrationItemType, MigrationItem> items = [];
			foreach (var item in migrationGroup)
			{
				if (items.ContainsKey(item.MigrationItemType))
				{
					throw new DuplicateMigrationItemException(migrationVersion, item.MigrationItemType);
				}
				MigrationItem migrationItem = new()
				{
					MigrationItemType = item.MigrationItemType,
					Name = item.Name,
					FileData = _fileSystem.ReadFileData(item.FilePath, _projectPath)
				};
				items.Add(item.MigrationItemType, migrationItem);
			}

			if (!items.ContainsKey(MigrationItemType.Migration))
			{
				throw new MigrationItemMustExistsException(migrationVersion);
			}

			Migration migration = new()
			{
				Version = migrationGroup.Key,
				Items = items
			};
			migrations.Add(migration);
		}

		// sort by version to ensure stable order
		return migrations.OrderBy(m => m.Version).ToList().AsReadOnly();
	}

	public IReadOnlyCollection<TestItem> GetTests()
	{
		string testsPath = _projectPath.CombineWith(_projectSettings.TestsFolder);
		string codePath = _projectPath.CombineWith(_projectSettings.CodeFolder);

		List<TestItem> testGroups = [];

		if (_fileSystem.PathExists(testsPath))
		{
			var testInTestFolder = _fileSystem.EnumerateDirectories(testsPath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testInTestFolder.Union([testsPath]))
			{
				testGroups.AddRange(GetFolderTests(folderPath, true));
			}
		}

		if (_fileSystem.PathExists(codePath))
		{
			var testsInCodeFolder = _fileSystem.EnumerateDirectories(codePath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testsInCodeFolder.Union([codePath]))
			{
				testGroups.AddRange(GetFolderTests(folderPath, false));
			}
		}

		return testGroups.AsReadOnly();
	}

	internal List<TestItem> GetFolderTests(string folderPath, bool isTestsFolder)
	{
		if (!_fileSystem.PathExists(folderPath)) return [];

		List<TestItem> tests = [];

		FileData? initFileData = null;
		string initializeFilePath = folderPath.CombineWith("init.sql");
		if (_fileSystem.FileExists(initializeFilePath))
		{
			initFileData = _fileSystem.ReadFileData(initializeFilePath, _projectPath);
		}

		// in Tests folder, any .sql file is considered as a test (except 'init.sql').
		var testFiles = _fileSystem.EnumerateFiles(
			folderPath,
			isTestsFolder ? ["*.sql"] : _projectSettings.TestFilePatterns, 
			["init.sql"], 
			subfolders: false
		);

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

		string folderPath = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => _projectSettings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => _projectSettings.AfterDeployFolder,
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
		return vsProjectPathAccessor.VisualStudioProjectPath;
	}
}
