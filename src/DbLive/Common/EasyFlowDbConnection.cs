namespace EasyFlow.Common;

public class EasyFlowDbConnection(string connectionString) : IEasyFlowDbConnection
{
	public string ConnectionString { get; } = connectionString;
}
