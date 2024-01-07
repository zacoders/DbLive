namespace EasyFlow.Common;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; set; } = TransactionWrapLevel.Migration;

	public TranIsolationLevel TransactionIsolationLevel { get; set; } = TranIsolationLevel.Serializable;

	public List<string> TestFilePatterns { get; set; } = ["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"];

	public TranIsolationLevel TestsTransactionIsolationLevel { get; set; } = TranIsolationLevel.Serializable;
}
