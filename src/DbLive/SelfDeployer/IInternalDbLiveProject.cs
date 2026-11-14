

namespace DbLive.SelfDeployer;

internal interface IInternalDbLiveProject
{
	Task<IReadOnlyList<InternalMigration>> GetMigrationsAsync();
}
