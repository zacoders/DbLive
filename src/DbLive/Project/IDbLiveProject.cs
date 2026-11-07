namespace DbLive.Project;

/// <summary>
/// The scripts of the project, read once per builder like settings.json: files added, deleted or changed after the first read
/// are not seen. A builder deploys one build.
/// </summary>
public interface IDbLiveProject
{
	/// <summary>
	/// Gets a read-only list of all available migrations in the current context. Migrations are ordered by their version number in ascending order.
	/// </summary>
	/// <returns>A read-only list of <see cref="Migration"/> objects representing the available migrations. The list is empty if no
	/// migrations are found.</returns>
	Task<IReadOnlyList<Migration>> GetMigrationsAsync();

	/// <summary>
	/// Code groups in the deployment order: one per entry of CodeSubFoldersDeploymentOrder, then the rest of the code.
	/// Items of a group are sorted by relative path, ordinal, ignoring case. Empty when the project has no code.
	/// </summary>
	Task<IReadOnlyList<CodeGroup>> GetCodeGroupsAsync();

	/// <summary>
	/// Breaking scripts of the Code folder and its subfolders (<c>breaking.sql</c>, <c>b.sql</c>, <c>*.breaking.sql</c>, <c>*.b.sql</c>),
	/// sorted by relative path, ordinal, ignoring case. They are not code items and not tests.
	/// </summary>
	Task<IReadOnlyList<GenericItem>> GetCodeBreakingItemsAsync();

	/// <summary>
	/// Tests of the Tests folder, then tests of the Code folder, each sorted by relative path, ordinal, ignoring case.
	/// </summary>
	Task<IReadOnlyCollection<TestItem>> GetTestsAsync();

	/// <summary>
	/// Gets list of items from the folder and its subfolders.
	/// </summary>
	/// <param name="projectFolder"></param>
	/// <returns>Read only list of items. Items sorted by relative path, ordinal, ignoring case.</returns>
	Task<ReadOnlyCollection<GenericItem>> GetFolderItemsAsync(ProjectFolder projectFolder);
}