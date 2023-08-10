namespace EasyFlow;

public interface IEasyFlowProject
{
	HashSet<MigrationTask> GetMigrationTasks(string migrationFolder);
	IEnumerable<Migration> GetProjectMigrations(string projectPath);
}