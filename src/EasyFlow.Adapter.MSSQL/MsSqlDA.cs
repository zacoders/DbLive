namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDA : IEasyFlowDA
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

	public bool EasyFlowInstalled(string cnnString)
	{
		const string query = @"
			select iif(object_id('easyflow.Migrations', 'U') is null, 0, 1)
		";

		using (var cnn = new SqlConnection(cnnString))
		{
			return cnn.ExecuteScalar<bool>(query);
		}
	}
}