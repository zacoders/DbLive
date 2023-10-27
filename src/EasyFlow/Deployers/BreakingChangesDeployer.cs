namespace EasyFlow;

public class BreakingChangesDeployer 
{
	private static readonly ILogger Logger = Log.ForContext(typeof(BreakingChangesDeployer));

	private readonly IEasyFlowDA _da;
	private readonly IEasyFlowProject _project;
	private readonly IEasyFlowPaths _paths;
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromDays(1);


	public BreakingChangesDeployer(IEasyFlowProject easyFlowProject, IEasyFlowDA easyFlowDA, IEasyFlowPaths paths)
	{
		_project = easyFlowProject;
		_da = easyFlowDA;
		_paths = paths;
	}

	public void DeployBreakingChanges(string sqlConnectionString, DeployParameters parameters)
	{
		if (!parameters.DeployBreaking)
		{
			return;
		}

		var breakingToApply = GetBreakingChangesToApply(sqlConnectionString, parameters);

		foreach (var breakingMigration in breakingToApply)
		{
			DeployBreakingMigration(breakingMigration, sqlConnectionString);
		}
	}

	private void DeployBreakingMigration(object breakingMigration, string sqlConnectionString)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<object> GetBreakingChangesToApply(string sqlConnectionString, DeployParameters parameters)
	{
		throw new NotImplementedException();
	}
}
