namespace EasyFlow.Project;

public interface IEasyFlowProject
{
	void Load(string projectPath);
	EasyFlowSettings GetSettings();
	HashSet<MigrationItem> GetMigrationItems(string migrationFolder);
	IEnumerable<Migration> GetMigrations();
	IEnumerable<CodeItem> GetCodeItems();
	IReadOnlyCollection<TestItem> GetTests();
}