namespace EasyFlow.Project;

public interface IEasyFlowProject
{
	//todo: switch from IEnumerable result to readonly collections?
	IEnumerable<Migration> GetMigrations();
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