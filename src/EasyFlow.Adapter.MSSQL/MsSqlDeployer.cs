namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowSqlConnection OpenConnection(string cnnString)
	{
		SqlConnection cnn = new(cnnString);
		cnn.Open();

		ServerConnection serverCnn = new(cnn);
		serverCnn.StatementTimeout = (int)TimeSpan.FromDays(30).TotalSeconds;

		return new MsSqlConnection(serverCnn);
	}
}