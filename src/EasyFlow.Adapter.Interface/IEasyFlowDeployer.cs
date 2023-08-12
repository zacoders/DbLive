namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDeployer
{
	IEasyFlowSqlConnection OpenConnection(string cnnString);
}