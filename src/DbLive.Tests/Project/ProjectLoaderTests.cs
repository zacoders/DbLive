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

	/// <summary>
	/// The loader of the files of <see cref="MockSet.ScriptSource"/>, through the real <see cref="DbLiveProject"/>.
	/// </summary>
	private static ProjectLoader CreateLoaderOfScriptSource(MockSet mockSet) =>
		new(mockSet.CreateUsingMocks<DbLiveProject>(), mockSet.SettingsAccessor, mockSet.TransactionSettingsValidator);
}
