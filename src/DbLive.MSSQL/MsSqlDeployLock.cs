using DbLive.Common;
using DbLive.Deployers;

namespace DbLive.MSSQL;

internal sealed class MsSqlDeployLock(IDbLiveDbConnection connection) : IDeployLock
{
	public async Task<IDeployLockHandle> AcquireAsync(string resourceName, CancellationToken cancellationToken = default)
	{
		SqlConnection sqlConnection = new(connection.ConnectionString);
		await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		SqlTransaction transaction = sqlConnection.BeginTransaction();

		try
		{
			const string sql = """
				declare @result int;
				exec @result = sp_getapplock
				    @Resource = @resource,
				    @LockMode = 'Exclusive',
				    @LockOwner = 'Transaction',
				    @LockTimeout = -1;
				select @result;
				""";

			int result = await sqlConnection.ExecuteScalarAsync<int>(
				new CommandDefinition(
					sql,
					new { resource = resourceName },
					transaction,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait(false);

			if (result < 0)
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				await sqlConnection.DisposeAsync().ConfigureAwait(false);
				throw new DeployLockFailedException(
					$"Failed to acquire deploy lock. sp_getapplock returned {result}.");
			}

			return new MsSqlDeployLockHandle(sqlConnection, transaction);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			await sqlConnection.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private sealed class MsSqlDeployLockHandle(SqlConnection connection, SqlTransaction transaction) : IDeployLockHandle
	{
		private bool _committed;

		public async Task CommitAsync()
		{
			if (_committed)
			{
				return;
			}

			await transaction.CommitAsync().ConfigureAwait(false);
			_committed = true;
		}

		public async ValueTask DisposeAsync()
		{
			if (!_committed && transaction.Connection is not null)
			{
				await transaction.RollbackAsync().ConfigureAwait(false);
			}

			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}
}
