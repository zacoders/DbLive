namespace DbLive.Deployers;

public interface IDeployLock
{
	Task<IDeployLockHandle> AcquireAsync(string resourceName, CancellationToken cancellationToken = default);
}

public interface IDeployLockHandle : IAsyncDisposable
{
	Task CommitAsync();
}
