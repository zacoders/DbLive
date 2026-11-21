namespace DbLive.Adapter;

/// <summary>
/// A script ran <c>COMMIT</c> or <c>ROLLBACK</c> and ended the transaction of DbLive that it ran in. No server lets DbLive keep a script
/// from it: the transaction belongs to the session, and the script runs in that session. The data access finds it by the ID of the
/// transaction, read before the script and after it, and rolls back what the script ran after the end; what a <c>COMMIT</c> of the
/// script committed stays. A caller does not retry such a script: it ends the transaction on every run.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class DbLiveTransactionEndedException(string example)
	: DbLiveSqlException(
		$"The script ended the transaction that DbLive runs it in: it ran COMMIT or ROLLBACK, {example}. "
		+ "DbLive rolled back what the script ran after that; what a COMMIT of the script committed stays in the database. "
		+ "Remove COMMIT and ROLLBACK from the script. A migration that must commit by itself needs \"TransactionWrapLevel\": \"None\" "
		+ "in its settings file."
	)
{
}
