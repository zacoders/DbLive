
namespace DbLive.Project;

public class DbLiveProject(
	IProjectPath projectPath,
	IFileSystem _fileSystem,
	ISettingsAccessor settingsAccessor,
	IVsProjectPathAccessor vsProjectPathAccessor
) : IDbLiveProject
{
	private readonly string _projectPath = projectPath.Path;
	
	public async Task<IReadOnlyList<CodeGroup>> GetCodeGroupsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync();
		
		string codePath = _projectPath.CombineWith(projectSettings.CodeFolder);
				
		var subPaths = projectSettings.CodeSubFoldersDeploymentOrder
			.Select(codePath.CombineWith)
			.ToList();

		List<string> codeFiles =
		[
			.. _fileSystem.EnumerateFiles(
					codePath,
					["*.sql"],
					projectSettings.TestFilePatterns,
					true
				)
		];

		var result = new List<CodeGroup>();

		foreach (string subPath in subPaths)
		{
			var files = codeFiles
				.RemoveWhere(f =>
					f.StartsWith(
						subPath + Path.DirectorySeparatorChar,
						StringComparison.OrdinalIgnoreCase
					));

			result.Add(new CodeGroup
			{
				Path = subPath,
				CodeItems = await GetCodeGroupItemsAsync(files)
			});
		}

		// everything else as a separate group
		result.Add(new CodeGroup
		{
			Path = codePath,
			CodeItems = await GetCodeGroupItemsAsync(codeFiles)
		});

		return result;
	}

	internal async Task<List<CodeItem>> GetCodeGroupItemsAsync(List<string> codeFiles)
	{
		List<CodeItem> codeItems = [];

		foreach (string filePath in codeFiles)
		{
			string fileName = Path.GetFileName(filePath);
			var codeItem = new CodeItem { Name = fileName, FileData = await _fileSystem.ReadFileDataAsync(filePath, _projectPath) };
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	public async Task<IReadOnlyList<Migration>> GetMigrationsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync();
		
		string migrationsPath = _projectPath.CombineWith(projectSettings.MigrationsFolder);

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
					FileData = await _fileSystem.ReadFileDataAsync(item.FilePath, _projectPath)
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

	public async Task<IReadOnlyCollection<TestItem>> GetTestsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync();
		
		string testsPath = _projectPath.CombineWith(projectSettings.TestsFolder);
		string codePath = _projectPath.CombineWith(projectSettings.CodeFolder);

		List<TestItem> testGroups = [];

		if (_fileSystem.PathExists(testsPath))
		{
			var testInTestFolder = _fileSystem.EnumerateDirectories(testsPath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testInTestFolder.Union([testsPath]))
			{
				testGroups.AddRange(await GetFolderTestsAsync(folderPath, true));
			}
		}

		if (_fileSystem.PathExists(codePath))
		{
			var testsInCodeFolder = _fileSystem.EnumerateDirectories(codePath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testsInCodeFolder.Union([codePath]))
			{
				testGroups.AddRange(await GetFolderTestsAsync(folderPath, false));
			}
		}

		return testGroups.AsReadOnly();
	}

	internal async Task<List<TestItem>> GetFolderTestsAsync(string folderPath, bool isTestsFolder)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync();
		
		if (!_fileSystem.PathExists(folderPath)) return [];

		List<TestItem> tests = [];

		FileData? initFileData = null;
		string initializeFilePath = folderPath.CombineWith("init.sql");
		if (_fileSystem.FileExists(initializeFilePath))
		{
			initFileData = await _fileSystem.ReadFileDataAsync(initializeFilePath, _projectPath);
		}

		// in Tests folder, any .sql file is considered as a test (except 'init.sql').
		var testFiles = _fileSystem.EnumerateFiles(
			folderPath,
			isTestsFolder ? ["*.sql"] : projectSettings.TestFilePatterns,
			["init.sql"],
			subfolders: false
		);

		foreach (string testFilePath in testFiles)
		{
			TestItem testItem = new()
			{
				Name = Path.GetFileName(testFilePath),
				FileData = await _fileSystem.ReadFileDataAsync(testFilePath, _projectPath),
				InitFileData = initFileData
			};

			tests.Add(testItem);
		}

		return tests;
	}

	/// <inheritdoc/>
	public async Task<ReadOnlyCollection<GenericItem>> GetFolderItemsAsync(ProjectFolder projectFolder)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync();
		
		Dictionary<string, GenericItem> items = [];

		string folderPath = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => projectSettings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => projectSettings.AfterDeployFolder,
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
		};

		string fullPath = Path.Combine(_projectPath, folderPath);

		if (_fileSystem.PathExists(fullPath))
		{
			var files = _fileSystem.EnumerateFiles(fullPath, "*.sql", true);
			foreach (string filePath in files)
			{
				string fileName = Path.GetFileName(filePath);
				var item = new GenericItem { Name = fileName, FileData = await _fileSystem.ReadFileDataAsync(filePath, _projectPath) };
				items.Add(filePath, item);
			}
		}

		// sorting the items by full path which is in the key of the dictionary.
		return items.OrderBy(i => i.Key).Select(i => i.Value).ToList().AsReadOnly();
	}

	[ExcludeFromCodeCoverage]
	public async Task<string> GetVisualStudioProjectPathAsync()
	{
		return await vsProjectPathAccessor.GetVisualStudioProjectPathAsync();
	}

}
