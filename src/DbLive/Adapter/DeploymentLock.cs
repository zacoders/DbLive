using System.Data.Common;

namespace DbLive.Adapter;

/// <summary>
/// The deployment lock that <see cref="IDbLiveDA.AcquireDeploymentLockAsync"/> took, on a connection of its own.
/// Disposing it releases the lock and closes the connection.
/// </summary>
internal sealed class DeploymentLock(DbConnection connection, Func<DbConnection, Task> release) : IAsyncDisposable
{
	public async ValueTask DisposeAsync()
	{
		try
		{
			// not left to the closing of the connection: the server ends the session of a closed connection asynchronously,
			// and a deployment that starts right after would find the lock still held.
			await release(connection).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// the connection is broken: its session ends with it, and the lock with the session. Not thrown: it would replace
			// the error of the deployment.
		}
		finally
		{
			await connection.DisposeAsync().ConfigureAwait(false);
		}
	}
}
