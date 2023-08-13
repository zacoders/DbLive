namespace EasyFlow.Project.Settings;

public class EasyFlowSettings
{
	public TransactionLevel TransactionLevel { get; set; } = TransactionLevel.Migration;
	public TransactionIsolationLevel TransactionIsolationLevel { get; set; } = TransactionIsolationLevel.Serializable;
}
