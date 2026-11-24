namespace DbLive.Common;

internal enum TransactionWrapLevel
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
	/// Migration and breaking scripts run without a transaction, unless their item settings say <see cref="Migration"/>.
	/// Each code file, each test and the self-deployment still run in a transaction of their own.
	/// </summary>
	None
}