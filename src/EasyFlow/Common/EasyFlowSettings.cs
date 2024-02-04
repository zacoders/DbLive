namespace EasyFlow.Common;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; set; } = TransactionWrapLevel.Migration;

	public TranIsolationLevel TransactionIsolationLevel { get; set; } = TranIsolationLevel.Serializable;

	public List<string> TestFilePatterns { get; set; } = ["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"];

	public TranIsolationLevel TestsTransactionIsolationLevel { get; set; } = TranIsolationLevel.Serializable;

	public string BeforeDeployFolder { get; set; } = "BeforeDeploy";
	public string AfterDeployFolder { get; set; } = "AfterDeploy";
	
	public string CodeFolder { get; set; } = "Code";
	public IEnumerable<string> CodeSubFoldersDeploymentOrder { get; set; } = Enumerable.Empty<string>();

	public string TestsFolder { get; set; } = "Tests";
}
