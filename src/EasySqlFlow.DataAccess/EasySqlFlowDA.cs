namespace EasySqlFlow.DataAccess;

public class EasySqlFlowDA : IEasySqlFlowDA
{
	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		const string query = @"
			select MigrationId
				 , MigrationName
				 , MigrationStarted
				 , MigrationCompleted
			from easyflow.Migrations
			order by MigrationId, MigrationName
		";

		using (var cnn = new SqlConnection(cnnString))
		{
			return cnn.Query<MigrationDto>(query).ToList();
		}
	}
}