namespace DbLive.Project;

public interface IDbLiveProject
{
	/// <summary>
	/// Gets a read-only list of all available migrations in the current context. Migrations are ordered by their version number in ascending order.
	/// </summary>
	/// <returns>A read-only list of <see cref="Migration"/> objects representing the available migrations. The list is empty if no
	/// migrations are found.</returns>
	IReadOnlyList<Migration> GetMigrations();

	IEnumerable<CodeGroup> GetCodeGroups();

	IReadOnlyCollection<TestItem> GetTests();

	/// <summary>
	/// Gets list of items from the folder. Items ordered alphabetically by the path.
	/// </summary>
	/// <param name="projectFolder"></param>
	/// <returns>Read only list of items. Items sorted by full file path.</returns>
	ReadOnlyCollection<GenericItem> GetFolderItems(ProjectFolder projectFolder);
	string GetVisualStudioProjectPath();
}