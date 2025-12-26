namespace DbLive.Common;

public enum TransactionWrapLevel
{
	/// <summary>
	/// Full deployment will be wrapped withing transaction.
	/// </summary>
	Deployment,

	/// <summary>
	/// Each migration wrapped by transaction.
	/// </summary>
	Migration,

	/// <summary>
	/// No transaction will be used.
	/// </summary>
	None
}