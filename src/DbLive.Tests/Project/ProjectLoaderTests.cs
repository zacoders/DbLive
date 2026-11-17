namespace DbLive.Tests.Project;

public class ProjectLoaderTests
{
	[Fact]
	public async Task Loads_every_part_of_the_project()
	{
		MockSet mockSet = new();

		IReadOnlyList<Migration> migrations = [];
		IReadOnlyList<CodeGroup> codeGroups = [];
		IReadOnlyList<GenericItem> codeBreakingItems = [];
		ReadOnlyCollection<GenericItem> beforeDeployItems = new([]);
		ReadOnlyCollection<GenericItem> afterDeployItems = new([]);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(migrations);
		mockSet.DbLiveProject.GetCodeGroupsAsync().Returns(codeGroups);
		mockSet.DbLiveProject.GetCodeBreakingItemsAsync().Returns(codeBreakingItems);
		mockSet.DbLiveProject.GetFolderItemsAsync(ProjectFolder.BeforeDeploy).Returns(beforeDeployItems);
		mockSet.DbLiveProject.GetFolderItemsAsync(ProjectFolder.AfterDeploy).Returns(afterDeployItems);

		ProjectLoader loader = mockSet.CreateUsingMocks<ProjectLoader>();

		ProjectSnapshot project = await loader.LoadAsync();

		Assert.Same(migrations, project.Migrations);
		Assert.Same(codeGroups, project.CodeGroups);
		Assert.Same(codeBreakingItems, project.CodeBreakingItems);
		Assert.Same(beforeDeployItems, project.BeforeDeployItems);
		Assert.Same(afterDeployItems, project.AfterDeployItems);

		// the tests are not deployed: DbLiveTester reads them.
		await mockSet.DbLiveProject.DidNotReceive().GetTestsAsync();

		mockSet.TransactionSettingsValidator.Received(1).ValidateMigrations(migrations);
	}

	[Fact]
	public async Task Isolation_level_of_a_settings_file_not_supported_by_the_provider_fails_the_load()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.PostgreSql);
		mockSet.ScriptSource.AddFiles(
			"Migrations/001.migration.sql",
			"Migrations/002.migration.sql",
			"Migrations/002.breaking.sql"
		);
		mockSet.ScriptSource.AddFile("Migrations/002.breaking.settings.json", """{ "TransactionIsolationLevel": "Snapshot" }""");

		ProjectLoader loader = new(
			mockSet.CreateUsingMocks<DbLiveProject>(),
			mockSet.SettingsAccessor,
			new TransactionSettingsValidator(mockSet.DbLiveDA, mockSet.Logger)
		);

		NotSupportedTransactionIsolationLevelException ex = await Assert.ThrowsAsync<NotSupportedTransactionIsolationLevelException>(loader.LoadAsync);

		Assert.Contains("Migrations/002.breaking.settings.json", ex.Message);
	}

	[Fact]
	public async Task Transaction_settings_are_checked_first()
	{
		MockSet mockSet = new();
		DbLiveSettings settings = new() { TransactionIsolationLevel = TranIsolationLevel.Snapshot };
		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(settings);
		mockSet.TransactionSettingsValidator.ValidateAsync(settings)
			.Returns(Task.FromException(new NotSupportedTransactionIsolationLevelException(TranIsolationLevel.Snapshot)));

		ProjectLoader loader = mockSet.CreateUsingMocks<ProjectLoader>();

		_ = await Assert.ThrowsAsync<NotSupportedTransactionIsolationLevelException>(loader.LoadAsync);

		Assert.Empty(mockSet.DbLiveProject.ReceivedCalls());
	}

	[Fact]
	public async Task Undo_file_in_the_project_fails_the_load()
	{
		MockSet mockSet = new();
		mockSet.ScriptSource.AddFile("Migrations/001.migration.sql", "create table t1 (id int);");
		mockSet.ScriptSource.AddFile("Migrations/001.undo.sql", "drop table t1;");

		ProjectLoader loader = CreateLoaderOfScriptSource(mockSet);

		UndoNotSupportedException ex = await Assert.ThrowsAsync<UndoNotSupportedException>(loader.LoadAsync);

		Assert.Equal("Migrations/001.undo.sql", ex.RelativePath);
	}

	[Fact]
	public async Task File_outside_the_project_folders_fails_the_load()
	{
		MockSet mockSet = new();
		mockSet.ScriptSource.AddFile("Migrations/001.migration.sql", "create table t1 (id int);");
		mockSet.ScriptSource.AddFile("README.md", "# Scripts");

		ProjectLoader loader = CreateLoaderOfScriptSource(mockSet);

		_ = await Assert.ThrowsAsync<FilesOutsideProjectFoldersException>(loader.LoadAsync);
	}

	[Theory]
	[InlineData("Migrations/001.migration.sql")]
	[InlineData("Code/GetUser.sql")]
	[InlineData("Code/GetUser.breaking.sql")]
	[InlineData("BeforeDeploy/before.sql")]
	[InlineData("AfterDeploy/after.sql")]
	public async Task File_that_cannot_be_read_fails_the_load(string relativePath)
	{
		MockSet mockSet = new();
		mockSet.ScriptSource.AddFiles(
			"Migrations/001.migration.sql",
			"Code/GetUser.sql",
			"Code/GetUser.breaking.sql",
			"BeforeDeploy/before.sql",
			"AfterDeploy/after.sql",
			"Tests/t1.sql"
		);
		IOException readError = new("The file is locked.");
		mockSet.ScriptSource.FailNextRead(relativePath, readError);

		ProjectLoader loader = CreateLoaderOfScriptSource(mockSet);

		IOException ex = await Assert.ThrowsAsync<IOException>(loader.LoadAsync);

		Assert.Same(readError, ex);
	}

	[Fact]
	public async Task Tests_are_not_read()
	{
		MockSet mockSet = new();
		mockSet.ScriptSource.AddFiles("Migrations/001.migration.sql", "Tests/t1.sql");

		ProjectLoader loader = CreateLoaderOfScriptSource(mockSet);

		_ = await loader.LoadAsync();

		Assert.Equal(1, mockSet.ScriptSource.ReadCount("Migrations/001.migration.sql"));
		Assert.Equal(0, mockSet.ScriptSource.ReadCount("Tests/t1.sql"));
	}

	[Theory]
	[InlineData("settings.json")]
	[InlineData("Migrations/001.migration.sql")]
	[InlineData("Migrations/001.migration.settings.json")]
	[InlineData("Code/GetUser.sql")]
	[InlineData("Code/GetUser.breaking.sql")]
	[InlineData("BeforeDeploy/before.sql")]
	[InlineData("AfterDeploy/after.sql")]
	public async Task File_that_is_not_utf8_fails_the_load(string relativePath)
	{
		using TempProject project = CreateProjectWithNonAsciiText();

		// the same files in UTF-8 load.
		_ = await CreateLoaderOfFolder(new MockSet(), project.Path).LoadAsync();

		SaveAsWindows1252(project, relativePath);

		FileNotUtf8Exception ex = await Assert.ThrowsAsync<FileNotUtf8Exception>(CreateLoaderOfFolder(new MockSet(), project.Path).LoadAsync);

		Assert.Equal(relativePath, ex.RelativePath);
	}

	[Theory]
	[InlineData("Tests/t1.sql")]
	[InlineData("Tests/setup.test.sql")]
	[InlineData("Code/GetUser.test.sql")]
	public async Task Test_file_that_is_not_utf8_fails_when_the_tests_are_read(string relativePath)
	{
		using TempProject project = CreateProjectWithNonAsciiText();
		SaveAsWindows1252(project, relativePath);

		FolderScriptSource scriptSource = new(project.Path);
		SettingsAccessor settingsAccessor = new(scriptSource);
		MockSet mockSet = new();
		DbLiveProject dbLiveProject = new(scriptSource, settingsAccessor, mockSet.MigrationVersionValidator, mockSet.Logger);

		// the deployment does not read the tests.
		_ = await new ProjectLoader(dbLiveProject, settingsAccessor, mockSet.TransactionSettingsValidator).LoadAsync();

		FileNotUtf8Exception ex = await Assert.ThrowsAsync<FileNotUtf8Exception>(dbLiveProject.GetTestsAsync);

		Assert.Equal(relativePath, ex.RelativePath);
	}

	private const string NonAsciiText = "Résumé reçu";

	/// <summary>
	/// A project in UTF-8 with <see cref="NonAsciiText"/> in every file.
	/// </summary>
	private static TempProject CreateProjectWithNonAsciiText() => new TempProject()
		.AddFile("settings.json", $$"""{ "ProjectId": "{{NonAsciiText}}" }""")
		.AddFile("Migrations/001.migration.sql", $"insert into dbo.Status (Id, Name) values (1, N'{NonAsciiText}');")
		.AddFile("Migrations/001.migration.settings.json", $"// {NonAsciiText}\n{{ }}")
		.AddFile("Code/GetUser.sql", $"-- {NonAsciiText}")
		.AddFile("Code/GetUser.breaking.sql", $"-- {NonAsciiText}")
		.AddFile("Code/GetUser.test.sql", $"-- {NonAsciiText}")
		.AddFile("BeforeDeploy/before.sql", $"-- {NonAsciiText}")
		.AddFile("AfterDeploy/after.sql", $"-- {NonAsciiText}")
		.AddFile("Tests/setup.test.sql", $"-- {NonAsciiText}")
		.AddFile("Tests/t1.sql", $"-- {NonAsciiText}");

	/// <summary>
	/// Saves the file again in Windows-1252 without a BOM, as an old editor does.
	/// </summary>
	private static void SaveAsWindows1252(TempProject project, string relativePath)
	{
		string filePath = Path.Combine(project.Path, relativePath);
		File.WriteAllBytes(filePath, System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1252)!.GetBytes(File.ReadAllText(filePath)));
	}

	/// <summary>
	/// The loader of the files of a folder, through the real <see cref="FolderScriptSource"/>, <see cref="SettingsAccessor"/>
	/// and <see cref="DbLiveProject"/>.
	/// </summary>
	private static ProjectLoader CreateLoaderOfFolder(MockSet mockSet, string path)
	{
		FolderScriptSource scriptSource = new(path);
		SettingsAccessor settingsAccessor = new(scriptSource);

		return new(
			new DbLiveProject(scriptSource, settingsAccessor, mockSet.MigrationVersionValidator, mockSet.Logger),
			settingsAccessor,
			mockSet.TransactionSettingsValidator
		);
	}

	/// <summary>
	/// The loader of the files of <see cref="MockSet.ScriptSource"/>, through the real <see cref="DbLiveProject"/>.
	/// </summary>
	private static ProjectLoader CreateLoaderOfScriptSource(MockSet mockSet) =>
		new(mockSet.CreateUsingMocks<DbLiveProject>(), mockSet.SettingsAccessor, mockSet.TransactionSettingsValidator);
}
