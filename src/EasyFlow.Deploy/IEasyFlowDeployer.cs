namespace EasyFlow.Deploy;

public interface IEasyFlowDeployer
{
	public IEasyFlowTransaction BeginTransaction();

	public void ExecuteNonQuery(string sql);

	public void EndTransaction(IEasyFlowTransaction transaction);
}