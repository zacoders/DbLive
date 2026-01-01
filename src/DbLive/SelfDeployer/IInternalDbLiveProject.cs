

namespace DbLive.SelfDeployer;

internal interface IInternalDbLiveProject
{
	IReadOnlyList<Migration> GetMigrations();
}
