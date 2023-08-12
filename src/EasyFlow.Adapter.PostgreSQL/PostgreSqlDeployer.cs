using Npgsql;

namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowSqlConnection OpenConnection(string connectionString)
	{
		var con = new NpgsqlConnection(connectionString);
		con.Open();
		return new PostreSqlConnection(con);
	}
}