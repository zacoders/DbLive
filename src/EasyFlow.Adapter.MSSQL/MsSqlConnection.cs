namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlConnection : IEasyFlowSqlConnection
{
	private readonly SqlConnection _sqlConnection;

	public MsSqlConnection(SqlConnection sqlConnection)
	{
		_sqlConnection = sqlConnection;
	}

	public void ExecuteNonQuery(string sqlStatement)
	{
		ServerConnection serverConnection = new(_sqlConnection);
		HandleException(() => serverConnection.ExecuteNonQuery(sqlStatement));
	}

	public void Close()
	{
		HandleException(_sqlConnection.Close);
	}

	private static void HandleException(Action action)
	{
		HandleException(() => { action(); return 0; });
	}

	private static T HandleException<T>(Func<T> action)
	{
		try
		{
			return action();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}
	}

	public void MigrationCompleted(string domain, int migrationVersion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		string query = @"
			insert into easyflow.Migrations
			(
				Domain
			  , MigrationVersion
			  , MigrationName
			  , MigrationStarted
			  , MigrationCompleted
			)
			values (
				@Domain
			  , @MigrationVersion
			  , @MigrationName
			  , @MigrationStartedUtc
			  , @MigrationCompletedUtc
			)
		";

		HandleException(() =>
			_sqlConnection.Query(query, new
			{
				domain,
				migrationVersion,
				migrationName,
				migrationStartedUtc,
				migrationCompletedUtc
			})
		);
	}

	public void Dispose()
	{
		try
		{
			// rolling all open transaction back
			_sqlConnection.Execute("while @@trancount > 0 rollback transaction");
		}
		catch { }
		try { _sqlConnection.Close(); } catch { }
		try { _sqlConnection.Dispose(); } catch { }
	}
}