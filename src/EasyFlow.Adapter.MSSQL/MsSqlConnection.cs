namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlConnection : IEasyFlowSqlConnection
{
	private readonly ServerConnection _serverConnection;

	public MsSqlConnection(ServerConnection serverCnn)
	{
		_serverConnection = serverCnn;
	}

	public void BeginTransaction(TransactionIsolationLevel isolationLevel)
	{
		HandleException(() =>
		{
			var isolationLevelStr = GetMsSqlIsolationLevel(isolationLevel);
			string sql = @$"
				set xact_abort on;
				set transaction isolation level {isolationLevelStr};
				begin transaction; 
			";
			_serverConnection.ExecuteNonQuery(sql);
		});
	}

	public void CommitTransaction()
	{
		HandleException(() => _serverConnection.ExecuteNonQuery("commit transaction"));
	}

	public void ExecuteNonQuery(string sqlStatementt)
	{
		HandleException(() => _serverConnection.ExecuteNonQuery(sqlStatementt));
	}

	public void Close()
	{
		HandleException(_serverConnection.Disconnect);
	}

	private static void HandleException(Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}
	}

	private string GetMsSqlIsolationLevel(TransactionIsolationLevel isolationLevel) =>
		isolationLevel switch
		{
			TransactionIsolationLevel.Chaos => "read uncommitted",
			TransactionIsolationLevel.ReadCommitted => "read committed",
			TransactionIsolationLevel.RepeatableRead => "repeatable read",
			TransactionIsolationLevel.Serializable => "serializable",
			_ => throw new NotSupportedTransactionIsolationLevelException(isolationLevel)
		};

	public void MigrationCompleted(int migrationVersion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		string query = @"
			insert into easyflow.Migrations
			(
				MigrationVersion
			  , MigrationName
			  , MigrationStarted
			  , MigrationCompleted
			)
			values ( 
				@MigrationVersion
			  , @MigrationName
			  , @MigrationStartedUtc
			  , @MigrationCompletedUtc
			)
		";

		HandleException(() =>
			_serverConnection.SqlConnectionObject.Query(query, new
			{
				migrationVersion,
				migrationName,
				migrationStartedUtc,
				migrationCompletedUtc
			})
		);
	}
}