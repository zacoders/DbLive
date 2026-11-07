using System.IO.Enumeration;

namespace DbLive.Project;

public class DbLiveProject(
	IScriptSource scriptSource,
	ISettingsAccessor settingsAccessor,
	IMigrationVersionValidator migrationVersionValidator,
	ILogger logger
) : IDbLiveProject
{
	/// <summary>
	/// Runs before each test in the same folder. Never deployed as code.
	/// </summary>
	internal const string TestSetupFileName = "setup.test.sql";

	/// <summary>
	/// Breaking scripts of the Code folder: run in the breaking phase, never deployed as code and never tests.
	/// </summary>
	internal static readonly IReadOnlyList<string> CodeBreakingFilePatterns = ["breaking.sql", "b.sql", "*.breaking.sql", "*.b.sql"];

	/// <summary>
	/// How many of the files outside the project folders <see cref="FilesOutsideProjectFoldersException"/> names.
	/// </summary>
	internal const int NamedOutsideFiles = 5;

	private readonly ILogger _logger = logger.ForContext(typeof(DbLiveProject));

	// a builder deploys one build: the project is read once per builder, like settings.json. The phases of a deployment ask
	// for the same migrations and code several times; the files are listed and checked once, and each file is read and hashed once.
	private readonly object _filesLock = new();
	private IReadOnlyList<string>? _files;
	private readonly ConcurrentDictionary<string, Task<FileData>> _fileData = new(StringComparer.Ordinal);

	public async Task<IReadOnlyList<CodeGroup>> GetCodeGroupsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string codeFolder = ToFolder(projectSettings.CodeFolder);

		List<string> codeFiles = GetFolderFiles(
			GetFiles(projectSettings),
			codeFolder,
			["*.sql"],
			[.. projectSettings.TestFilePatterns, TestSetupFileName, .. CodeBreakingFilePatterns]
		);

		if (codeFiles.Count == 0)
		{
			return [];
		}

		var result = new List<CodeGroup>();

		foreach (string subFolder in projectSettings.CodeSubFoldersDeploymentOrder)
		{
			string subFolderPath = Combine(codeFolder, ToFolder(subFolder));

			List<string> files = codeFiles.RemoveWhere(f => IsInFolder(f, subFolderPath));

			if (files.Count == 0)
			{
				_logger.Warning(
					"Code subfolder '{SubFolder}' from CodeSubFoldersDeploymentOrder matches no code files.",
					subFolder
				);
			}

			result.Add(new CodeGroup
			{
				Path = subFolderPath,
				CodeItems = await GetCodeGroupItemsAsync(files).ConfigureAwait(false)
			});
		}

		// everything else as a separate group
		result.Add(new CodeGroup
		{
			Path = codeFolder,
			CodeItems = await GetCodeGroupItemsAsync(codeFiles).ConfigureAwait(false)
		});

		return result;
	}

	private async Task<List<CodeItem>> GetCodeGroupItemsAsync(List<string> codeFiles)
	{
		List<CodeItem> codeItems = [];

		foreach (string relativePath in codeFiles)
		{
			var codeItem = new CodeItem
			{
				Name = Path.GetFileName(relativePath),
				FileData = await ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<GenericItem>> GetCodeBreakingItemsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		List<GenericItem> items = [];

		foreach (string relativePath in GetFolderFiles(GetFiles(projectSettings), ToFolder(projectSettings.CodeFolder), CodeBreakingFilePatterns, []))
		{
			var item = new GenericItem
			{
				Name = Path.GetFileName(relativePath),
				FileData = await ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};
			items.Add(item);
		}

		return items.AsReadOnly();
	}

	public async Task<IReadOnlyList<Migration>> GetMigrationsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		// a project without migrations (code only) is valid.
		List<string> migrationFiles = GetFolderFiles(
			GetFiles(projectSettings),
			ToFolder(projectSettings.MigrationsFolder),
			["*.sql", "*.json"],
			[]
		);

		List<MigrationItemInfo> migrationItems = [];

		foreach (string relativePath in migrationFiles)
		{
			MigrationItemInfo migrationItemInfo = MigrationFileNameParser.GetMigrationInfo(relativePath);

			if (projectSettings.EnforceTimestampVersion)
			{
				migrationVersionValidator.Validate(migrationItemInfo.Version, migrationItemInfo.RelativePath);
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
					FileData = await ReadFileDataAsync(item.RelativePath).ConfigureAwait(false)
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

		IReadOnlyList<string> files = GetFiles(projectSettings);

		List<string> testFiles =
		[
			// in Tests folder, any .sql file is considered as a test (except the setup file).
			.. GetFolderFiles(files, ToFolder(projectSettings.TestsFolder), ["*.sql"], [TestSetupFileName]),
			.. GetFolderFiles(files, ToFolder(projectSettings.CodeFolder), projectSettings.TestFilePatterns, [TestSetupFileName])
		];

		// the setup file of each folder, read once for all its tests.
		Dictionary<string, FileData?> setupFiles = new(StringComparer.OrdinalIgnoreCase);

		List<TestItem> tests = [];

		foreach (string relativePath in testFiles)
		{
			string folder = GetFolder(relativePath);

			if (!setupFiles.TryGetValue(folder, out FileData? setupFileData))
			{
				setupFileData = await ReadSetupFileAsync(files, folder).ConfigureAwait(false);
				setupFiles.Add(folder, setupFileData);
			}

			TestItem testItem = new()
			{
				Name = Path.GetFileName(relativePath),
				FileData = await ReadFileDataAsync(relativePath).ConfigureAwait(false),
				SetupFileData = setupFileData
			};

			tests.Add(testItem);
		}

		return tests.AsReadOnly();
	}

	private async Task<FileData?> ReadSetupFileAsync(IReadOnlyList<string> files, string folder)
	{
		string setupFilePath = Combine(folder, TestSetupFileName);

		string? setupFile = files.FirstOrDefault(f => f.Equals(setupFilePath, StringComparison.OrdinalIgnoreCase));

		return setupFile is null ? null : await ReadFileDataAsync(setupFile).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<ReadOnlyCollection<GenericItem>> GetFolderItemsAsync(ProjectFolder projectFolder)
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string folder = projectFolder switch
		{
			ProjectFolder.BeforeDeploy => projectSettings.BeforeDeployFolder,
			ProjectFolder.AfterDeploy => projectSettings.AfterDeployFolder,
			_ => throw new ArgumentOutOfRangeException(nameof(projectFolder), projectFolder, "Unknown ProjectFolded.")
		};

		List<GenericItem> items = [];

		foreach (string relativePath in GetFolderFiles(GetFiles(projectSettings), ToFolder(folder), ["*.sql"], []))
		{
			var item = new GenericItem
			{
				Name = Path.GetFileName(relativePath),
				FileData = await ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};
			items.Add(item);
		}

		return items.AsReadOnly();
	}

	private Task<FileData> ReadFileDataAsync(string relativePath) =>
		_fileData.GetOrAdd(relativePath, path => scriptSource.ReadFileDataAsync(path));

	private IReadOnlyList<string> GetFiles(DbLiveSettings projectSettings)
	{
		lock (_filesLock)
		{
			return _files ??= ReadFiles(projectSettings);
		}
	}

	/// <summary>
	/// All files of the project. Every read checks all of them, so the deployment stops at its first read (migrations),
	/// before anything is deployed, whatever folder the files are in. The files pass the checks once; files that fail them
	/// are checked again on the next read, and fail again.
	/// </summary>
	/// <exception cref="ScriptPathsDifferOnlyByCaseException">
	/// Two paths differ only by case (possible on Linux): DbLive finds files and identifies them in the database
	/// ignoring case, so they would be one item.
	/// </exception>
	/// <exception cref="FilesOutsideProjectFoldersException">
	/// A file is outside the project folders and is not the root settings.json, so DbLive would never read it.
	/// </exception>
	/// <exception cref="BreakingScriptMatchesTestPatternException">
	/// A file of the Code folder is named as a breaking script and matches TestFilePatterns (test.breaking.sql),
	/// so it would be both.
	/// </exception>
	private IReadOnlyList<string> ReadFiles(DbLiveSettings projectSettings)
	{
		IReadOnlyList<string> files = scriptSource.GetFiles();

		HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

		foreach (string file in files)
		{
			if (!paths.Add(file))
			{
				_ = paths.TryGetValue(file, out string? otherFile);
				throw new ScriptPathsDifferOnlyByCaseException(otherFile!, file);
			}
		}

		IReadOnlyList<string> projectFolders = GetProjectFolders(projectSettings);

		List<string> outsideFiles = [.. files.Where(f => IsOutsideProjectFolders(f, projectFolders)).Order(StringComparer.OrdinalIgnoreCase)];

		if (outsideFiles.Count > 0)
		{
			throw new FilesOutsideProjectFoldersException(outsideFiles.Count, [.. outsideFiles.Take(NamedOutsideFiles)], projectFolders);
		}

		string? breakingTestFile = GetFolderFiles(files, ToFolder(projectSettings.CodeFolder), CodeBreakingFilePatterns, [])
			.FirstOrDefault(f => MatchesAny(f, projectSettings.TestFilePatterns));

		if (breakingTestFile is not null)
		{
			throw new BreakingScriptMatchesTestPatternException(breakingTestFile);
		}

		return files;
	}

	/// <summary>
	/// The files of the folder and its subfolders whose names match one of the patterns and none of the exclude patterns,
	/// sorted by relative path the same way on every OS and in every culture.
	/// </summary>
	private static List<string> GetFolderFiles(
		IReadOnlyList<string> files,
		string folder,
		IEnumerable<string> patterns,
		IEnumerable<string> excludePatterns
	)
	{
		return
		[
			.. files
				.Where(f => IsInFolder(f, folder) && MatchesAny(f, patterns) && !MatchesAny(f, excludePatterns))
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
		];
	}

	/// <summary>
	/// The folders DbLive reads scripts from, as relative paths.
	/// </summary>
	private static IReadOnlyList<string> GetProjectFolders(DbLiveSettings projectSettings) =>
	[
		ToFolder(projectSettings.MigrationsFolder),
		ToFolder(projectSettings.CodeFolder),
		ToFolder(projectSettings.TestsFolder),
		ToFolder(projectSettings.BeforeDeployFolder),
		ToFolder(projectSettings.AfterDeployFolder)
	];

	/// <summary>
	/// A file DbLive never reads: outside the project folders and not the root settings.json.
	/// Hidden files and folders (.DS_Store, .gitkeep, .vs/) are skipped: tools create them without asking.
	/// </summary>
	private static bool IsOutsideProjectFolders(string relativePath, IReadOnlyList<string> projectFolders) =>
		!projectFolders.Any(folder => IsInFolder(relativePath, folder))
		&& !relativePath.Equals(SettingsAccessor.SettingsFileName, StringComparison.OrdinalIgnoreCase)
		&& !relativePath.Split('/').Any(name => name.StartsWith('.'));

	/// <summary>
	/// A folder from the settings ("Code", "db\Code", "/Code/") as a relative path: "Code", "db/Code".
	/// </summary>
	private static string ToFolder(string folder) => folder.Replace('\\', '/').Trim('/');

	private static string Combine(string folder, string relativePath) =>
		folder.Length == 0 ? relativePath
		: relativePath.Length == 0 ? folder
		: $"{folder}/{relativePath}";

	private static bool IsInFolder(string relativePath, string folder) =>
		folder.Length == 0
		|| (relativePath.Length > folder.Length
			&& relativePath[folder.Length] == '/'
			&& relativePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase));

	private static string GetFolder(string relativePath)
	{
		int separatorIndex = relativePath.LastIndexOf('/');
		return separatorIndex < 0 ? "" : relativePath[..separatorIndex];
	}

	/// <summary>
	/// Matches the file name the way Directory.EnumerateFiles does on Windows, ignoring case on every OS.
	/// </summary>
	private static bool MatchesAny(string relativePath, IEnumerable<string> patterns)
	{
		string fileName = Path.GetFileName(relativePath);

		return patterns.Any(pattern =>
			FileSystemName.MatchesWin32Expression(FileSystemName.TranslateWin32Expression(pattern), fileName, ignoreCase: true));
	}
}
