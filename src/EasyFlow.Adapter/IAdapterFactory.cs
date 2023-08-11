namespace EasyFlow.Adapter;

public interface IAdapterFactory
{
	public IEasyFlowDeployer GetDeployer(DBEngine dbEngine);
}