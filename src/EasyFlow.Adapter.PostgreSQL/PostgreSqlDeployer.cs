namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer : IEasyFlowDeployer
{
	public void CreateDB(string cnnString, bool skipIfExists = true)
	{
		throw new NotImplementedException();
	}

	public IEasyFlowSqlConnection OpenConnection(string connectionString)
	{
		var con = new NpgsqlConnection(connectionString);
		con.Open();
		return new PostreSqlConnection(con);
	}
}