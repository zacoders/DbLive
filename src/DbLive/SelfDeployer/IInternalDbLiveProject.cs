

namespace DbLive.SelfDeployer;

internal interface IInternalDbLiveProject
{
	IEnumerable<Migration> GetMigrations();
}
