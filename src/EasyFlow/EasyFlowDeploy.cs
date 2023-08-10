namespace EasyFlow;

public class EasyFlowDeploy
{
	private readonly IEasyFlowDA _easyFlowDA;
	private readonly IEasyFlowProject _easyFlowProject;

	public EasyFlowDeploy(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA)
	{
		_easyFlowProject = easyFlowProject;
		_easyFlowDA = easyFlowDA;
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
		var appliedMigrations = _easyFlowDA.GetMigrations(sqlConnectionString);
		int appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);

		string migrationsPath = Path.Combine(proejctPath, "Migrations");

		var migrationsToApply = _easyFlowProject.GetProjectMigrations(migrationsPath)
			.Where(m =>
				   appliedVersion == 0
				|| m.Version > appliedVersion
				// additional check for the migrations with the same version but different name 
				|| (m.Version == appliedVersion
					 && !appliedMigrations.Any(am => am.MigrationVersion == appliedVersion && am.MigrationName == m.Name))
			)
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
		return migrationsToApply;
	}

	private void DeployMigration(Migration migration)
	{
		Console.WriteLine(migration.PathUri);
		var tasks = _easyFlowProject.GetMigrationTasks(migration.PathUri.LocalPath);
		foreach (var task in tasks)
		{
			Console.WriteLine(task.MigrationType);
		}
	}

}
