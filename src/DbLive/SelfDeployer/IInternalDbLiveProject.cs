

namespace DbLive.SelfDeployer;

public interface IInternalDbLiveProject
{
	Task<IReadOnlyList<InternalMigration>> GetMigrationsAsync();
}
