namespace EasyFlow.Common;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; init; } = TransactionWrapLevel.Migration;

	public TranIsolationLevel TransactionIsolationLevel { get; init; } = TranIsolationLevel.Serializable;

	public List<string> TestFilePatterns { get; init; } = ["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"];

	public TranIsolationLevel TestsTransactionIsolationLevel { get; init; } = TranIsolationLevel.Serializable;

	public string BeforeDeployFolder { get; init; } = "BeforeDeploy";
	public string AfterDeployFolder { get; init; } = "AfterDeploy";
	
	public string CodeFolder { get; init; } = "Code";
	public IEnumerable<string> CodeSubFoldersDeploymentOrder { get; init; } = Enumerable.Empty<string>();

	public string TestsFolder { get; init; } = "Tests";

	public TimeSpan DeploymentTimeout { get; init; } = TimeSpan.FromDays(1);
	public TimeSpan MigrationTimeout { get; init; } = TimeSpan.FromDays(1);
	public TimeSpan MigrationItemTimeout { get; init; } = TimeSpan.FromHours(12);
}
