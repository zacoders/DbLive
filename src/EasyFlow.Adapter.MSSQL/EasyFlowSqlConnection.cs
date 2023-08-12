namespace EasyFlow.Adapter.MSSQL;

internal class EasyFlowSqlConnection : IEasyFlowSqlConnection
{
	public ServerConnection ServerConnection { get; private set; }

	public EasyFlowSqlConnection(ServerConnection serverCnn)
	{
		ServerConnection = serverCnn;
	}
}