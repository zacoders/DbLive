namespace EasySqlFlow;

public class DeploySQL
{
	private readonly IEasySqlFlowDA _easySqlFlowDA;
	private readonly ISqlProject _sqlProject;

	public DeploySQL(ISqlProject sqlProject, IEasySqlFlowDA easySqlFlowDA)
	{
		_sqlProject = sqlProject;
		_easySqlFlowDA = easySqlFlowDA;
	}

	public void DeployProject(string proejctPath, string sqlConnectionString)
	{
		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(proejctPath, sqlConnectionString);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(migration);
		}
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(string proejctPath, string sqlConnectionString)
	{
		var appliedMigrations = _easySqlFlowDA.GetMigrations(sqlConnectionString);
		int appliedMigrationVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);

		string migrationsPath = Path.Combine(proejctPath, "Migrations");

		var migrationsToApply = _sqlProject.GetProjectMigrations(migrationsPath)
			.Where(m =>
				   appliedMigrationVersion == 0
				|| m.Version >= appliedMigrationVersion
				// additional check for the migrations with the same version but different name 
				|| (m.Version == appliedMigrationVersion && appliedMigrations.Any(am => am.MigrationVersion == appliedMigrationVersion && am.MigrationName == m.Name))
			)
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
		return migrationsToApply;
	}

	private void DeployMigration(Migration migration)
	{
		Console.WriteLine(migration.PathUri.GetLastSegment());
		var tasks = _sqlProject.GetMigrationTasks(migration.PathUri.LocalPath);
		foreach (var task in tasks)
		{
			Console.WriteLine(task.MigrationType);
		}
	}

}
