using EasyFlow.Adapter;

namespace EasyFlow;

public class EasyFlowInternalManager(
	IEasyFlowBuilder _builder,
	IEasyFlowPaths _paths
)
: IEasyFlowInternalManager
{
	public IEasyFlowInternal CreateEasyFlowInternal()
	{
		IEasyFlowInternal selfDeployer = _builder.CloneBuilder()
			.SetProjectPath(_paths.GetPathToEasyFlowSelfProject())
			.CreateSelfDeployer();

		return selfDeployer;
	}
}