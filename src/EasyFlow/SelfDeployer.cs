using EasyFlow.Adapter;

namespace EasyFlow;

public class SelfDeployer(
	IEasyFlowBuilder _builder,
	IEasyFlowPaths _paths
)
: ISelfDeployer
{
	public IEasyFlowInternal CreateEasyFlowSelf()
	{
		IEasyFlowInternal selfDeployer = _builder.CloneBuilder()
			.SetProjectPath(_paths.GetPathToEasyFlowSelfProject())
			.CreateSelfDeployer();

		return selfDeployer;
	}
}