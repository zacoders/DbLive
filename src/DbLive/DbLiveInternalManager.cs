using DbLive.Adapter;

namespace DbLive;

public class DbLiveInternalManager(
	IDbLiveBuilder _builder,
	IDbLivePaths _paths
)
: IDbLiveInternalManager
{
	public IDbLiveInternal CreateDbLiveInternal()
	{
		IDbLiveInternal selfDeployer = _builder.CloneBuilder()
			.SetProjectPath(_paths.GetPathToDbLiveSelfProject())
			.CreateSelfDeployer();

		return selfDeployer;
	}
}