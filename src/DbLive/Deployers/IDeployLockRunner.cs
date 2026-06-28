namespace DbLive.Deployers;

public interface IDeployLockRunner
{
	Task ExecuteWithLockAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
