namespace DbLive.Tests;

internal static class ProjectSnapshots
{
	/// <summary>
	/// A project without files. A test sets the parts it needs: <c>ProjectSnapshots.Empty with { Migrations = [...] }</c>.
	/// </summary>
	public static ProjectSnapshot Empty { get; } = new()
	{
		Migrations = [],
		CodeGroups = [],
		CodeBreakingItems = [],
		BeforeDeployItems = [],
		AfterDeployItems = []
	};
}
