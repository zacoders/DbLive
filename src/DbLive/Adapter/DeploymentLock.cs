using System.Data.Common;

namespace DbLive.Adapter;

/// <summary>
/// The deployment lock that <see cref="IDbLiveDA.AcquireDeploymentLockAsync"/> took, on a connection of its own.
/// Disposing it releases the lock and closes the connection, and then throws the error of the release, if any.
/// </summary>
internal sealed class DeploymentLock(DbConnection connection, Func<DbConnection, Task> release) : IAsyncDisposable
{
	/// <summary>
	/// The connection that holds the lock, for the tests.
	/// </summary>
	internal DbConnection Connection => connection;

	public async ValueTask DisposeAsync()
	{
		try
		{
			// not left to the closing of the connection: the server ends the session of a closed connection asynchronously,
			// and a deployment that starts right after would find the lock still held.
			await release(connection).ConfigureAwait(false);
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}
}
