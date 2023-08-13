using EasyFlow.Project.Settings;

namespace EasyFlow.Project;

public interface IEasyFlowProject
{
	void Load(string projectPath);
	EasyFlowSettings GetSettings();
	HashSet<MigrationTask> GetMigrationTasks(string migrationFolder);
	IEnumerable<Migration> GetProjectMigrations();
}