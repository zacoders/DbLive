
namespace DbLive.Project;

public class DbLiveProject(
	IProjectPath projectPath,
	ISettingsAccessor settingsAccessor,
	IMigrationVersionValidator migrationVersionValidator,
	ILogger logger
) : IDbLiveProject
{
	/// <summary>
	/// Runs before each test in the same folder. Never deployed as code.
	/// </summary>
	internal const string TestSetupFileName = "setup.test.sql";

	private readonly ILogger _logger = logger.ForContext(typeof(DbLiveProject));
	private readonly IFileSystem _fileSystem = projectPath.FileSystem;
	private readonly string _projectPath = projectPath.Path;
	private readonly string? _visualStudioProjectPath = projectPath.VisualStudioProjectPath;

	public async Task<IReadOnlyList<CodeGroup>> GetCodeGroupsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string codePath = _projectPath.CombineWithRelative(projectSettings.CodeFolder);

		if (!_fileSystem.PathExists(codePath))
		{
			return [];
		}

		List<string> codeFiles =
		[
			.. _fileSystem.EnumerateFiles(
					codePath,
					["*.sql"],
					[.. projectSettings.TestFilePatterns, TestSetupFileName],
					true
				)
		];

		var result = new List<CodeGroup>();

		foreach (string subFolder in projectSettings.CodeSubFoldersDeploymentOrder)
		{
			string subPath = codePath.CombineWithRelative(subFolder);

			List<string> files = codeFiles
				.RemoveWhere(f =>
					f.StartsWith(
						subPath + Path.DirectorySeparatorChar,
						StringComparison.OrdinalIgnoreCase
					));

			if (files.Count == 0)
			{
				_logger.Warning(
					"Code subfolder '{SubFolder}' from CodeSubFoldersDeploymentOrder matches no code files.",
					subFolder
				);
			}

			result.Add(new CodeGroup
			{
				Path = subPath,
				CodeItems = await GetCodeGroupItemsAsync(files).ConfigureAwait(false)
			});
		}

		// everything else as a separate group
		result.Add(new CodeGroup
		{
			Path = codePath,
			CodeItems = await GetCodeGroupItemsAsync(codeFiles).ConfigureAwait(false)
		});

		return result;
	}

	internal async Task<List<CodeItem>> GetCodeGroupItemsAsync(List<string> codeFiles)
	{
		List<CodeItem> codeItems = [];

		foreach (string filePath in codeFiles)
		{
			string fileName = Path.GetFileName(filePath);
			var codeItem = new CodeItem { Name = fileName, FileData = await _fileSystem.ReadFileDataAsync(filePath, _projectPath).ConfigureAwait(false) };
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	public async Task<IReadOnlyList<Migration>> GetMigrationsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string migrationsPath = _projectPath.CombineWithRelative(projectSettings.MigrationsFolder);

		// a project without migrations (code only) is valid.
		if (!_fileSystem.PathExists(migrationsPath))
		{
			return [];
		}

		IEnumerable<string> migrationFiles = _fileSystem.EnumerateFiles(migrationsPath, ["*.sql", "*.json"], subfolders: true);

		List<MigrationItemInfo> migrationItems = [];

		foreach (string filePath in migrationFiles)
		{
			MigrationItemInfo migrationItemInfo = MigrationFileNameParser.GetMigrationInfo(filePath);

			if (projectSettings.EnforceTimestampVersion)
			{
				migrationVersionValidator.Validate(migrationItemInfo.Version, migrationItemInfo.FilePath);
			}

			migrationItems.Add(migrationItemInfo);
		}

		List<Migration> migrations = [];
		foreach (IGrouping<long, MigrationItemInfo> migrationGroup in migrationItems.ToLookup(i => i.Version))
		{
			long migrationVersion = migrationGroup.Key;
			Dictionary<MigrationItemType, MigrationItem> items = [];
			foreach (MigrationItemInfo item in migrationGroup)
			{
				if (items.ContainsKey(item.MigrationItemType))
				{
					throw new DuplicateMigrationItemException(migrationVersion, item.MigrationItemType);
				}
				MigrationItem migrationItem = new()
				{
					MigrationItemType = item.MigrationItemType,
					Name = item.Name,
					FileData = await _fileSystem.ReadFileDataAsync(item.FilePath, _projectPath).ConfigureAwait(false)
				};
				items.Add(item.MigrationItemType, migrationItem);
			}

			if (!items.ContainsKey(MigrationItemType.Migration))
			{
				throw new MigrationItemMustExistsException(migrationVersion);
			}

			foreach (MigrationItem settingsItem in items.Values.Where(i => MigrationItemTypeMapper.IsSettings(i.MigrationItemType)))
			{
				MigrationItemType scriptType = MigrationItemTypeMapper.GetScriptType(settingsItem.MigrationItemType);
				if (!items.ContainsKey(scriptType))
				{
					throw new MigrationSettingsWithoutScriptException(settingsItem.FileData.RelativePath, migrationVersion, scriptType);
				}

				// parsing here, so an invalid settings file stops the deployment before anything is deployed.
				_ = SettingsTools.GetMigrationItemSettings(settingsItem.FileData.Content, settingsItem.FileData.RelativePath);
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
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string testsPath = _projectPath.CombineWithRelative(projectSettings.TestsFolder);
		string codePath = _projectPath.CombineWithRelative(projectSettings.CodeFolder);

		List<TestItem> testGroups = [];

		if (_fileSystem.PathExists(testsPath))
		{
			IEnumerable<string> testInTestFolder = _fileSystem.EnumerateDirectories(testsPath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testInTestFolder.Union([testsPath]))
			{
				testGroups.AddRange(await GetFolderTestsAsync(folderPath, true).ConfigureAwait(false));
			}
		}

		if (_fileSystem.PathExists(codePath))
		{
			IEnumerable<string> testsInCodeFolder = _fileSystem.EnumerateDirectories(codePath, "*", SearchOption.AllDirectories);
			foreach (var folderPath in testsInCodeFolder.Union([codePath]))
			{
				testGroups.AddRange(await GetFolderTestsAsync(folderPath, false).ConfigureAwait(false));
			}
		}

		return testGroups.AsReadOnly();
	}

	internal async Task<List<TestItem>> GetFolderTestsAsync(string folderPath, bool isTestsFolder)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		if (!_fileSystem.PathExists(folderPath)) return [];

		List<TestItem> tests = [];

		FileData? setupFileData = null;
		string setupFilePath = folderPath.CombineWith(TestSetupFileName);
		if (_fileSystem.FileExists(setupFilePath))
		{
			setupFileData = await _fileSystem.ReadFileDataAsync(setupFilePath, _projectPath).ConfigureAwait(false);
		}

		// in Tests folder, any .sql file is considered as a test (except the setup file).
		IEnumerable<string> testFiles = _fileSystem.EnumerateFiles(
			folderPath,
			isTestsFolder ? ["*.sql"] : projectSettings.TestFilePatterns,
			[TestSetupFileName],
			subfolders: false
		);

		foreach (string testFilePath in testFiles)
		{
			TestItem testItem = new()
			{
				Name = Path.GetFileName(testFilePath),
				FileData = await _fileSystem.ReadFileDataAsync(testFilePath, _projectPath).ConfigureAwait(false),
				SetupFileData = setupFileData
			};

			tests.Add(testItem);
		}

		return tests;
	}

	/// <inheritdoc/>
	public async Task<ReadOnlyCollection<GenericItem>> GetFolderItemsAsync(ProjectFolder projectFolder)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		Dictionary<string, GenericItem> items = [];

		string folderPath = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => projectSettings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => projectSettings.AfterDeployFolder,
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
		};

		string fullPath = _projectPath.CombineWithRelative(folderPath);

		if (_fileSystem.PathExists(fullPath))
		{
			IEnumerable<string> files = _fileSystem.EnumerateFiles(fullPath, "*.sql", true);
			foreach (string filePath in files)
			{
				string fileName = Path.GetFileName(filePath);
				var item = new GenericItem { Name = fileName, FileData = await _fileSystem.ReadFileDataAsync(filePath, _projectPath).ConfigureAwait(false) };
				items.Add(filePath, item);
			}
		}

		// sorting the items by full path which is in the key of the dictionary.
		return items.OrderBy(i => i.Key).Select(i => i.Value).ToList().AsReadOnly();
	}

	[ExcludeFromCodeCoverage]
	public string? GetVisualStudioProjectPath()
	{
		return _visualStudioProjectPath;
	}
}
