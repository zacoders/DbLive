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

	private readonly ILogger _logger = logger.ForContext(typeof(DbLiveProject));

	public async Task<IReadOnlyList<CodeGroup>> GetCodeGroupsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		string codeFolder = ToFolder(projectSettings.CodeFolder);

		List<string> codeFiles = GetFolderFiles(
			scriptSource.GetFiles(),
			codeFolder,
			["*.sql"],
			[.. projectSettings.TestFilePatterns, TestSetupFileName]
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
				FileData = await scriptSource.ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};
			codeItems.Add(codeItem);
		}

		return codeItems;
	}

	public async Task<IReadOnlyList<Migration>> GetMigrationsAsync()
	{
		DbLiveSettings projectSettings = await settingsAccessor.GetProjectSettingsAsync().ConfigureAwait(false);

		// a project without migrations (code only) is valid.
		List<string> migrationFiles = GetFolderFiles(
			scriptSource.GetFiles(),
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
					FileData = await scriptSource.ReadFileDataAsync(item.RelativePath).ConfigureAwait(false)
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

		IReadOnlyList<string> files = scriptSource.GetFiles();

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
				FileData = await scriptSource.ReadFileDataAsync(relativePath).ConfigureAwait(false),
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

		return setupFile is null ? null : await scriptSource.ReadFileDataAsync(setupFile).ConfigureAwait(false);
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

		foreach (string relativePath in GetFolderFiles(scriptSource.GetFiles(), ToFolder(folder), ["*.sql"], []))
		{
			var item = new GenericItem
			{
				Name = Path.GetFileName(relativePath),
				FileData = await scriptSource.ReadFileDataAsync(relativePath).ConfigureAwait(false)
			};
			items.Add(item);
		}

		return items.AsReadOnly();
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
				// files that differ only by case, possible on Linux.
				.ThenBy(f => f, StringComparer.Ordinal)
		];
	}

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
