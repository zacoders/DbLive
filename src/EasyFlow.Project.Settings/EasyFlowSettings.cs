namespace EasyFlow.Project.Settings;

public class EasyFlowSettings
{
	public TransactionWrapLevel TransactionWrapLevel { get; set; } = TransactionWrapLevel.Migration;
	public TransactionIsolationLevel TransactionIsolationLevel { get; set; } = TransactionIsolationLevel.Serializable;
}
