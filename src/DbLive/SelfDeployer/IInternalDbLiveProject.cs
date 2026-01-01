

namespace DbLive.SelfDeployer;

public interface IInternalDbLiveProject
{
	IReadOnlyList<InternalMigration> GetMigrations();
}
