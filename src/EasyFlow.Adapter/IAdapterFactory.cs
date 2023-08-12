namespace EasyFlow.Adapter;

public interface IAdapterFactory
{
	public IEasyFlowSqlConnection GetDeployer(DBEngine dbEngine, string connectionString);
}