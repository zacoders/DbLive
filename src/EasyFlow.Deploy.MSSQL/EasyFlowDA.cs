using Dapper;
using Microsoft.Data.SqlClient;

namespace EasyFlow.Adapter.MSSQL;

public class EasyFlowDA : IEasyFlowDA
{
	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		const string query = @"
			select MigrationVersion
				 , MigrationName
				 , MigrationStarted
				 , MigrationCompleted
			from easyflow.Migrations
		";

		using (var cnn = new SqlConnection(cnnString))
		{
			return cnn.Query<MigrationDto>(query).ToList();
		}
	}
}