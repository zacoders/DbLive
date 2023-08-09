namespace EasySqlFlow;

public interface ISqlProject
{
	HashSet<MigrationTask> GetMigrationTasks(string migrationFolder);
	IEnumerable<Migration> GetProjectMigrations(string projectPath);
}