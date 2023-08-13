namespace EasyFlow.Project.Settings;

public enum TransactionLevel
{
	/// <summary>
	/// Full deployment will be wrapped withing transaction
	/// </summary>
	Deployment,

	/// <summary>
	/// Each migration wrapped by transaction
	/// </summary>
	Migration,

	/// <summary>
	/// Migration task wrapped by transaction
	/// </summary>
	Task,

	/// <summary>
	/// No transactions introduced by EasySqlFlow
	/// </summary>
	None
}