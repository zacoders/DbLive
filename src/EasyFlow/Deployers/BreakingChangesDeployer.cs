namespace EasyFlow.Deployers;

public class BreakingChangesDeployer
{
	private static readonly ILogger Logger = Log.ForContext(typeof(BreakingChangesDeployer));

	private readonly IEasyFlowDA _da;
	private readonly MigrationItemDeployer _migrationItemDeployer;
	private readonly IEasyFlowProject _project;

	public BreakingChangesDeployer(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowPaths paths, MigrationItemDeployer migrationItemDeployer)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_migrationItemDeployer = migrationItemDeployer;
	}

	public void DeployBreakingChanges(string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		DeployBreakingMigration(sqlConnectionString, parameters);
	}

	private void DeployBreakingMigration(string sqlConnectionString, DeployParameters parameters)
	{
		Dictionary<Tuple<int, string>, MigrationItemDto> breakingToApply = _da.GetNonAppliedBreakingMigrationItems(sqlConnectionString)
			.ToDictionary(i => new Tuple<int, string>(i.Version, i.Name));

		int minVersionOfMigration = breakingToApply.Min(b => b.Value.Version);
		
		var migrations = _project.GetMigrations().Where(m => m.Version >= minVersionOfMigration);

		foreach (var migration in migrations)
		{
			if (!breakingToApply.Keys.Contains(new Tuple<int, string>(migration.Version, migration.Name))) continue;

			//todo: validate checksum of the breaking changes, throw error if check summ is different?
			var breakingChnagesItems = _project.GetMigrationItems(migration.FolderPath)
				.Where(mi => mi.MigrationType == MigrationItemType.BreakingChange);

			foreach(MigrationItem breaking in breakingChnagesItems)
			{
				_migrationItemDeployer.DeployMigrationItem(sqlConnectionString, false, migration, breaking, new[] { MigrationItemType.BreakingChange });
			}
		}
	}
}
