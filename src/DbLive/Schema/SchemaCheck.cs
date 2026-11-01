namespace DbLive.Schema;

/// <summary>
/// The schema checks of the deployment tests (<c>[SqlDeployFact]</c>). Production deployments do not run them.
/// </summary>
public enum SchemaCheck
{
	/// <summary>The schema after the undo script equals the schema before the migration.</summary>
	Undo,

	/// <summary>The second run of a migration breaking script does not change the schema.</summary>
	BreakingSecondRun,

	/// <summary>
	/// A second breaking deployment does not change the schema: the breaking scripts of the Code folder
	/// and the BeforeDeploy/AfterDeploy scripts run again.
	/// </summary>
	BreakingSecondDeployment
}
