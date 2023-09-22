
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

		using var cnn = new SqlConnection(cnnString);
		return cnn.Query<MigrationDto>(query).ToList();
	}

	public bool EasyFlowInstalled(string cnnString)
	{
		const string query = @"
			select iif(object_id('easyflow.Migrations', 'U') is null, 0, 1)
		";

		using var cnn = new SqlConnection(cnnString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public int GetEasyFlowVersion(string cnnString)
	{
		const string query = @"
			select Version
			from easyflow.Version
		";

		using var cnn = new SqlConnection(cnnString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public void SetEasyFlowVersion(string cnnString, int version, DateTime migrationDatetime)
	{
		const string query = @"
			merge into easyflow.Version as t
			using ( select 1 ) as s(c) on 1 = 1
			when not matched then 
				insert ( Version, MigrationDatetime ) values ( @Version, @MigrationDatetime )
			when matched then update
				set Version = @Version
			      , MigrationDatetime = MigrationDatetime;
		";

		using var cnn = new SqlConnection(cnnString);
		cnn.Query(query, new { version, migrationDatetime });
	}

	public void MigrationCompleted(string cnnString, int migrationVersion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		string query = @"
			insert into easyflow.Migrations
			(
				MigrationVersion
			  , MigrationName
			  , MigrationStarted
			  , MigrationCompleted
			)
			values (
				@MigrationVersion
			  , @MigrationName
			  , @MigrationStartedUtc
			  , @MigrationCompletedUtc
			)
		";

		using var cnn = new SqlConnection(cnnString);
		cnn.Query(query, new
		{
			migrationVersion,
			migrationName,
			migrationStartedUtc,
			migrationCompletedUtc
		});
	}

}