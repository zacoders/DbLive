namespace DbLive.Adapter;

/// <summary>
/// A script began a transaction and did not commit it: <c>BEGIN TRANSACTION</c> without <c>COMMIT</c>, in the script or in a procedure
/// it called. Nothing fails then: the commit of DbLive only lowers the count of open transactions, the connection goes back to the pool
/// with the transaction open, and the server rolls it back when the session is reused. The deployment would succeed, and what the
/// script did would be gone. The data access finds it by the count of open transactions after the script and rolls the transaction back
/// itself. A caller does not retry such a script: it leaves the transaction open on every run.
/// </summary>
/// <param name="rolledBack">What DbLive rolled back, and what of the script stays.</param>
[ExcludeFromCodeCoverage]
internal sealed class DbLiveTransactionLeftOpenException(string rolledBack)
	: DbLiveSqlException(
		"The script left a transaction open: it ran BEGIN TRANSACTION without COMMIT, by itself or in a procedure it called. "
		+ "The server would roll it back unnoticed when the connection is reused. "
		+ $"{rolledBack} Commit every transaction that the script begins."
	)
{
}
