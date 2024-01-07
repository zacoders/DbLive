using System.Collections.ObjectModel;

namespace EasyFlow.Project;

public interface IEasyFlowProject
{
	EasyFlowSettings GetSettings();
	ReadOnlyCollection<MigrationItem> GetMigrationItems(string migrationFolder);
	IEnumerable<Migration> GetMigrations();
	IEnumerable<CodeItem> GetCodeItems();
	IReadOnlyCollection<TestItem> GetTests();
}