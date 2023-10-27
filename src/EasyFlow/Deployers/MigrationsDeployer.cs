namespace EasyFlow.Deployers;

public class MigrationsDeployer
{
	private static readonly ILogger Logger = Log.ForContext(typeof(MigrationsDeployer));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowProject _project;

	private EasyFlowSettings _projectSettings = new();
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);

	public MigrationsDeployer(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
	}

	public void DeployMigrations(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployMigrations)
		{
			return;
		}

		IOrderedEnumerable<Migration> migrationsToApply = GetMigrationsToApply(isSelfDeploy, sqlConnectionString, parameters);

		foreach (var migration in migrationsToApply)
		{
			DeployMigration(isSelfDeploy, migration, sqlConnectionString);
		}
	}

	// todo: should be moved to sepate class? it should be private, but we need to unit test it.
	public IOrderedEnumerable<Migration> GetMigrationsToApply(bool isSelfDeploy, string sqlConnectionString, DeployParameters parameters)
	{
		int appliedVersion = 0;
		IReadOnlyCollection<MigrationDto> appliedMigrations = Array.Empty<MigrationDto>();

		if (_da.EasyFlowInstalled(sqlConnectionString))
		{
			if (isSelfDeploy)
			{
				appliedVersion = _da.GetEasyFlowVersion(sqlConnectionString);
			}
			else
			{
				appliedMigrations = _da.GetMigrations(sqlConnectionString);
				appliedVersion = appliedMigrations.Count == 0 ? 0 : appliedMigrations.Max(m => m.Version);
			}
		}

		if (isSelfDeploy)
		{
			return _project.GetMigrations()
				.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
				.Where(m => m.Version > appliedVersion)
				.OrderBy(m => m.Version)
				.ThenBy(m => m.Name);
		}

		return _project.GetMigrations()
			.Where(m => m.Version <= (parameters.MaxVersionToDeploy ?? int.MaxValue))
			.Where(m => !appliedMigrations.Any(am => am.Version == m.Version && am.Name == m.Name))
			.OrderBy(m => m.Version)
			.ThenBy(m => m.Name);
	}

	private void DeployMigration(bool isSelfDeploy, Migration migration, string sqlConnectionString)
	{
		Logger.Information(migration.FolderPath.GetLastSegment());
		var migrationItems = _project.GetMigrationItems(migration.FolderPath);

		if (migrationItems.Count == 0) return;

		DateTime migrationStartedUtc = DateTime.UtcNow;

		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Migration,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //toto: separate timeout for all migrations
			() =>
			{
				foreach (MigrationItem migrationItem in migrationItems.OrderBy(t => t.MigrationType))
				{
					DeployMigrationItem(sqlConnectionString, isSelfDeploy, migration, migrationItem);
				}

				DateTime migrationCompletedUtc = DateTime.UtcNow;

				if (isSelfDeploy)
				{
					_da.SetEasyFlowVersion(sqlConnectionString, migration.Version, migrationCompletedUtc);
				}
				else
				{
					int durationMs = (int)(migrationCompletedUtc - migrationStartedUtc).TotalMilliseconds;
					_da.MarkMigrationAsApplied(sqlConnectionString, migration.Version, migration.Name, migrationCompletedUtc, durationMs);
				}
			}
		);
	}

	private void DeployMigrationItem(string sqlConnectionString, bool isSelfDeploy, Migration migration, MigrationItem migrationItem)
	{
		Transactions.ExecuteWithinTransaction(
			_projectSettings.TransactionWrapLevel == TransactionWrapLevel.Task,
			_projectSettings.TransactionIsolationLevel,
			_defaultTimeout, //todo: separate timeout for single migration
			() =>
			{
				Logger.Information("Migration {migrationType}", migrationItem.MigrationType);

				DateTime migrationStartedUtc = DateTime.UtcNow;

				string status = "";
				DateTime? migrationAppliedUtc = null;
				int? executionTimeMs = null;
				if (migrationItem.MigrationType.In(MigrationType.Migration, MigrationType.Data))
				{
					_da.ExecuteNonQuery(sqlConnectionString, migrationItem.FileData.Content);
					status = "applied";
					migrationAppliedUtc = DateTime.UtcNow;
					executionTimeMs = (int)(migrationAppliedUtc.Value - migrationStartedUtc).TotalMilliseconds;
				}
				else
				{
					status = "skipped";
				}

				if (!isSelfDeploy)
				{
					_da.SaveMigrationItemState(
						sqlConnectionString,
						migration.Version,
						migration.Name,
						migrationItem.MigrationType.ToString().ToLower(),
						migrationItem.FileData.Crc32Hash,
						status,
						DateTime.UtcNow,
						migrationAppliedUtc,
						executionTimeMs
					);
				}

				Logger.Information("Migration {migrationType} {status}.", migrationItem.MigrationType, status);
			}
		);
	}
}
