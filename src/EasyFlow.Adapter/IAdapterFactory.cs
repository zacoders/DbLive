namespace EasyFlow.Adapter;

internal interface IAdapterFactory
{
	public IEasyFlowDeployer GetDeployer(DBEngine dbEngine);
}