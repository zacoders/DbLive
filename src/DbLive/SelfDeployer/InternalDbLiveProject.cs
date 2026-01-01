

namespace DbLive.SelfDeployer;

internal class InternalDbLiveProject : IInternalDbLiveProject
{
	public IEnumerable<Migration> GetMigrations() => throw new NotImplementedException();
}