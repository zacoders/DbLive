namespace EasyFlow.Project.Settings;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; set; } = TransactionWrapLevel.Migration;
	public TranIsolationLevel TransactionIsolationLevel { get; set; } = TranIsolationLevel.Serializable;
	public string TestFilePattern { get; set; } = "*test.sql";
}
