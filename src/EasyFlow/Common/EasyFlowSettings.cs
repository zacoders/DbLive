namespace EasyFlow.Common;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; init; } = TransactionWrapLevel.Migration;

	public TranIsolationLevel TransactionIsolationLevel { get; init; } = TranIsolationLevel.Serializable;

	public List<string> TestFilePatterns { get; init; } = ["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"];

	public TranIsolationLevel TestsTransactionIsolationLevel { get; init; } = TranIsolationLevel.Serializable;

	public string BeforeDeployFolder { get; init; } = "BeforeDeploy";
	public string AfterDeployFolder { get; init; } = "AfterDeploy";
}
