namespace DbLive.Common;

public enum TransactionWrapLevel
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
	/// Migration item wrapped by transaction
	/// </summary>
	MigrationItem,

	/// <summary>
	/// No transactions introduced by EasySqlFlow
	/// </summary>
	None
}