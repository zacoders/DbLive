using EasyFlow.Project.Settings;

namespace EasyFlow;

public class EasyFlow : IEasyFlow
{
	private readonly IEasyFlowDA _easyFlowDA;
	private readonly IEasyFlowDeployer _easyFlowDeployer;
	private readonly IEasyFlowProject _easyFlowProject;
	private EasyFlowSettings _easyFlowProjectSettings = new EasyFlowSettings();

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
		DeployProjectInternal("self", easyFlowSqlSource, sqlConnectionString, int.MaxValue);

		// Deploy actuall project
		DeployProjectInternal("project", proejctPath, sqlConnectionString, maxVersion);
	}

	private void DeployProjectInternal(string domain, string proejctPath, string sqlConnectionString, int maxVersion)
	{
		_easyFlowProject.Load(proejctPath);
		_easyFlowProjectSettings = _easyFlowProject.GetSettings();

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(domain, sqlConnectionString, maxVersion);

		IEasyFlowSqlConnection cnn = _easyFlowDeployer.OpenConnection(sqlConnectionString);

		if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Deployment)
			cnn.BeginTransaction(TransactionIsolationLevel.Serializable);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(domain, migration, cnn);
		}

		if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Deployment)
			cnn.CommitTransaction();
	}

	public IOrderedEnumerable<Migration> GetMigrationsToApply(string domain, string sqlConnectionString, int maxVersion)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_easyFlowDA.EasyFlowInstalled(sqlConnectionString))
		{
			appliedMigrations = _easyFlowDA.GetMigrations(domain, sqlConnectionString);
			appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.MigrationVersion);
		}

		var migrationsToApply = _easyFlowProject.GetProjectMigrations()
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

	private void DeployMigration(string domain, Migration migration, IEasyFlowSqlConnection cnn)
	{
		Console.WriteLine("");
		Console.WriteLine(migration.PathUri.GetLastSegment());
		var tasks = _easyFlowProject.GetMigrationTasks(migration.PathUri.LocalPath);

		DateTime migrationStartedUtc = DateTime.UtcNow;

		if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Migration)
			cnn.BeginTransaction(TransactionIsolationLevel.Serializable);

		foreach (MigrationTask task in tasks.OrderBy(t => t.MigrationType))
		{
			if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Task)
				cnn.BeginTransaction(TransactionIsolationLevel.Serializable);

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

			if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Task)
				cnn.CommitTransaction();
		}

		if (_easyFlowProjectSettings.TransactionLevel == TransactionLevel.Migration)
			cnn.CommitTransaction();

		DateTime migrationCompletedUtc = DateTime.UtcNow;

		cnn.MigrationCompleted(domain, migration.Version, migration.Name, migrationStartedUtc, migrationCompletedUtc);

		cnn.CommitTransaction();
	}
}
