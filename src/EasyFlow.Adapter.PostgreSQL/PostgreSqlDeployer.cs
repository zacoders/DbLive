using Npgsql;

namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer
{
	public static IEasyFlowSqlConnection OpenConnection(string connectionString)
	{
		var con = new NpgsqlConnection(connectionString: connectionString);
		con.Open();
		return new PostreSqlConnection(con);
	}
}