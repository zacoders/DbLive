
namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDA : IEasyFlowDA
{
	public void CreateDB(string cnnString, bool skipIfExists = true)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(cnnString);

		string? databaseToCreate = cnnBuilder.Database;
		if (databaseToCreate == null) throw new ArgumentException("Database must be specified in connection string.");

		cnnBuilder.Database = "postgres";

		var cnn = new NpgsqlConnection(cnnBuilder.ConnectionString);
		cnn.Open();

		var cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from pg_catalog.pg_database where lower(datname) = lower(:name)";
		cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)cmd.ExecuteScalar() == 1;

		if (dbExists && skipIfExists) return;

		cnn.Execute($"""create database "{databaseToCreate}" """);
		cnn.Close();
	}

	public bool EasyFlowInstalled(string cnnString)
	{
		const string query = @"
			select exists ( select * from pg_tables where schemaname = 'easyflow' and tablename = 'Migrations'
		";

		using var cnn = new NpgsqlConnection(cnnString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public void ExecuteNonQuery(string cnnString, string sqlStatementt)
	{
		try
		{
			using var cnn = new NpgsqlConnection(cnnString);
			cnn.Open();
			var cmd = cnn.CreateCommand();
			cmd.CommandText = sqlStatementt;
			cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}
	}

	public int GetEasyFlowVersion(string cnnString)
	{
		const string query = @"
			select Version
			from easyflow.Version
		";

		using var cnn = new NpgsqlConnection(cnnString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		const string query = @"
			select MigrationVersion
				 , MigrationName
				 , MigrationStarted
				 , MigrationCompleted
			from easyflow.Migration
		";

		using var cnn = new NpgsqlConnection(cnnString);
		return cnn.Query<MigrationDto>(query).ToList();
	}

	public bool IsCodeItemApplied(string cnnString, string filePath, Guid contentMD5Hash)
	{
		throw new NotImplementedException();
	}

	public void MarkCodeAsApplied(string cnnString, string relativePath, Guid contentMD5Hash, DateTime createdUtc, int executionTimeMs)
	{
		throw new NotImplementedException();
	}

	public void MarkCodeAsVerified(string cnnString, string relativePath, DateTime verifiedUtc)
	{
		throw new NotImplementedException();
	}

	public void MarkMigrationAsApplied(string cnnString, int migrationVersion, string migrationName, DateTime migrationCompletedUtc, int executionTimeMs)
	{
		//todo: refactor tabe name and column names for postgres
		string query = @"
			insert into easyflow.Migration
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

		using var cnn = new NpgsqlConnection(cnnString);
		cnn.Query(query, new
		{
			migrationVersion,
			migrationName,
			migrationCompletedUtc,
			executionTimeMs
		});
	}

	public void SaveMigrationItemState(string cnnString, int version, string name, string migrationType, Guid contentMD5Hash, string status, DateTime createdUtc, DateTime? appliedUtc, int? executionTimeMs)
	{
		throw new NotImplementedException();
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

		using var cnn = new NpgsqlConnection(cnnString);
		cnn.Query(query, new { version, migrationDatetime });
	}

}
