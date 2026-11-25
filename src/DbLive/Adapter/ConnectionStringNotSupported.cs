namespace DbLive.Adapter;

/// <summary>
/// The errors of <see cref="IDbLiveDA.CheckConnectionString"/>: settings of the connection string with which every deployment (with a
/// code file, for <c>MultipleActiveResultSets=True</c>) or every run of the SQL tests fails, under an error of the provider that does not
/// name the setting.
/// </summary>
internal static class ConnectionStringNotSupported
{
	/// <summary>
	/// SQL Server only. DbLive opens a connection for each call of its data access, also several in one <c>TransactionScope</c>, and only
	/// the connection pool gives the next one the session of the transaction. Without the pool the next one is another session, the
	/// transaction is promoted to a distributed one, and .NET fails it. Npgsql keeps the session of the transaction also without the pool.
	/// </summary>
	public static NotSupportedException Pooling() => new(
		"The connection string sets Pooling=False, which DbLive does not support on SQL Server: DbLive opens several connections in one "
		+ "transaction, and only the connection pool gives them the session of the transaction. Without the pool the transaction becomes "
		+ "a distributed one, which .NET does not allow (\"Implicit distributed transactions have not been enabled\"). "
		+ "Remove Pooling=False from the connection string."
	);

	/// <summary>
	/// A connection that does not enlist runs outside the transactions of DbLive: its statements commit at once, and the check of a script
	/// in a transaction fails every script with <see cref="DbLiveTransactionEndedException"/>, which tells to remove a COMMIT that the
	/// script does not have.
	/// </summary>
	public static NotSupportedException Enlist() => new(
		"The connection string sets Enlist=False, which DbLive does not support: DbLive runs its scripts in transactions of "
		+ "System.Transactions (TransactionScope), and a connection that does not enlist runs outside them. "
		+ "Remove Enlist=False from the connection string."
	);

	/// <summary>
	/// SQL Server only. Under MARS a transaction begun with T-SQL (<c>BEGIN TRANSACTION</c>, or implicit) ends with its batch: SQL Server
	/// rolls it back with Msg 3997 when it is still active at the end of the batch. A code file runs its <c>GO</c> batches in a transaction
	/// that DbLive begins with a batch of its own, so every code file failed, after the migrations of the deployment were committed.
	/// The transactions of <c>TransactionScope</c> are begun through the client API and are not bound to a batch.
	/// </summary>
	public static NotSupportedException MultipleActiveResultSets() => new(
		"The connection string sets MultipleActiveResultSets=True, which DbLive does not support on SQL Server: with MARS a transaction "
		+ "begun with BEGIN TRANSACTION ends with its batch, and DbLive runs the GO batches of a code file in one such transaction, so "
		+ "every code file would fail with \"A transaction that was started in a MARS batch is still active at the end of the batch\". "
		+ "DbLive does not use MARS. Remove MultipleActiveResultSets=True from the connection string of DbLive."
	);
}
