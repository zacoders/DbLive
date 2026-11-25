namespace DbLive.Adapter;

/// <summary>
/// A script began a transaction and did not commit it: <c>BEGIN TRANSACTION</c> without <c>COMMIT</c>, on SQL Server also in a procedure
/// it called. Nothing fails then: the transaction goes back to the pool with the connection and is rolled back unnoticed, by SQL Server
/// when the session is reused, by Npgsql when the connection goes back to the pool. The deployment would succeed, and what the script did
/// would be gone. The data access finds it after the script and rolls the transaction back itself. A caller does not retry such a script:
/// it leaves the transaction open on every run.
/// </summary>
/// <param name="cause">What the script ran, which differs by server: on PostgreSQL a procedure cannot begin a transaction.</param>
/// <param name="rolledBack">What DbLive rolled back, and what of the script stays.</param>
[ExcludeFromCodeCoverage]
internal sealed class DbLiveTransactionLeftOpenException(string cause, string rolledBack)
	: DbLiveSqlException(
		$"The script left a transaction open: {cause}. "
		+ "It would be rolled back unnoticed with the connection. "
		+ $"{rolledBack} Commit every transaction that the script begins."
	)
{
}
