using DbLive.Common;
using DbLive.Deployers;

namespace DbLive.PostgreSQL;

internal sealed class PostgreSqlDeployLock(IDbLiveDbConnection connection) : IDeployLock
{
	public async Task<IDeployLockHandle> AcquireAsync(string resourceName, CancellationToken cancellationToken = default)
	{
		NpgsqlConnection pgConnection = new(connection.ConnectionString);
		await pgConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

		NpgsqlTransaction transaction = await pgConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			const string sql = "select pg_advisory_xact_lock(hashtext(@resource))";

			_ = await pgConnection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new { resource = resourceName },
					transaction,
					cancellationToken: cancellationToken
				)
			).ConfigureAwait(false);

			return new PostgreSqlDeployLockHandle(pgConnection, transaction);
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			await pgConnection.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private sealed class PostgreSqlDeployLockHandle(NpgsqlConnection connection, NpgsqlTransaction transaction) : IDeployLockHandle
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
