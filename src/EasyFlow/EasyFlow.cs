namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private readonly IEasyFlowDA _easyFlowDA;
	private readonly IEasyFlowDeployer _easyFlowDeployer;
	private readonly IEasyFlowProject _easyFlowProject;

	public EasyFlow(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowDeployer easyFlowDeployer)
	{
		_easyFlowProject = easyFlowProject;
		_easyFlowDA = easyFlowDA;
		_easyFlowDeployer = easyFlowDeployer;
	}
	public void DeployProject(string proejctPath, string sqlConnectionString, int maxVersion)
	{
		// Self deploy. Deploying EasyFlow to the database
		string easyFlowSqlSource = Path.Combine(AppContext.BaseDirectory, "EasyFlowSql");
		DeployProjectInternal(easyFlowSqlSource, sqlConnectionString, int.MaxValue);

		// Deploy actuall project
		DeployProjectInternal(proejctPath, sqlConnectionString, maxVersion);
	}

	private void DeployProjectInternal(string proejctPath, string sqlConnectionString, int maxVersion)
	{
		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(proejctPath, sqlConnectionString, maxVersion);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(migration, sqlConnectionString);
		}
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(string proejctPath, string sqlConnectionString, int maxVersion)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_easyFlowDA.EasyFlowInstalled(sqlConnectionString))
		{
			appliedMigrations = _easyFlowDA.GetMigrations(sqlConnectionString);
			appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);
		}

		string migrationsPath = Path.Combine(proejctPath, "Migrations");

		var migrationsToApply = _easyFlowProject.GetProjectMigrations(migrationsPath)
			.Where(m => m.Version <= maxVersion)
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

	private void DeployMigration(Migration migration, string sqlConnectionString)
	{
		Console.WriteLine("");
		Console.WriteLine(migration.PathUri.GetLastSegment());
		var tasks = _easyFlowProject.GetMigrationTasks(migration.PathUri.LocalPath);

		DateTime migrationStartedUtc = DateTime.UtcNow;
		var cnn = _easyFlowDeployer.OpenConnection(sqlConnectionString);

		cnn.BeginTransaction(TransactionIsolationLevel.ReadCommitted);
		
		foreach (MigrationTask task in tasks.OrderBy(t => t.MigrationType))
		{
			Console.WriteLine(task.MigrationType);
			if (new[] { MigrationType.Migration, MigrationType.Data }.Contains(task.MigrationType))
			{
				string sql = File.ReadAllText(task.FileUri.LocalPath);
				cnn.ExecuteNonQuery(sql);
			}
			else
			{
				Console.WriteLine("  - skipped");
			}
		}

		DateTime migrationCompletedUtc = DateTime.UtcNow;

		cnn.MigrationCompleted(migration.Version, migration.Name, migrationStartedUtc, migrationCompletedUtc);

		cnn.CommitTransaction();
	}
}
