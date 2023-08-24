namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDeployer
{
	IEasyFlowSqlConnection OpenConnection(string cnnString);

	void CreateDB(string cnnString, bool skipIfExists = true);
}