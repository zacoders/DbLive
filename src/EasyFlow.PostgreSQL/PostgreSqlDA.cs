using EasyFlow.Adapter;
using EasyFlow.Common;
using System.Data;

namespace EasyFlow.PostgreSQL;

public class PostgreSqlDA(IEasyFlowDbConnection _cnn) : IEasyFlowDA
{
	public void CreateDB(bool skipIfExists = true)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(_cnn.ConnectionString);

		string? databaseToCreate = cnnBuilder.Database ?? throw new ArgumentException("Database must be specified in connection string.");

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

	public bool EasyFlowInstalled()
	{
		const string query = @"
			select exists ( select * from pg_tables where schemaname = 'easyflow' and tablename = 'Migrations'
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public void ExecuteNonQuery(string sqlStatementt)
	{
		try
		{
			using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
			cnn.Open();
			var cmd = cnn.CreateCommand();
			cmd.CommandText = sqlStatementt;
			cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			var pgException = e.Get<PostgresException>();
			throw new EasyFlowSqlException(pgException?.Message ?? e.Message, e);
		}
	}

	public int GetEasyFlowVersion()
	{
		const string query = @"
			select Version
			from easyflow.Version
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations()
	{
		const string query = @"
			select version
				 , name
				 , created_utc
				 , modified_utc
			from easyflow.Migration
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationDto>(query).ToList();
	}

	public IReadOnlyCollection<MigrationItemDto> GetNonAppliedBreakingMigrationItems()
	{
		const string query = @"
			select version
				 , name
				 , item_type
				 , status
				 , content_hash
				 , content
				 , created_utc
				 , applied_utc
				 , execution_time_ms
			from easyflow.migration_item
			where status != 'applied'
			  and item_type = 'breakingchange'
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationItemDto>(query).ToList();
	}

	public CodeItemDto? FindCodeItem(string relativePath)
	{
		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.QueryFirstOrDefault<CodeItemDto>(
			"easyflow.get_code_item",
			new { relative_path = relativePath },
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkCodeAsApplied(string relativePath, int contentHash, DateTime createdUtc, int executionTimeMs)
	{
		throw new NotImplementedException();
	}

	public void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc)
	{
		throw new NotImplementedException();
	}

	public void SaveMigration(int migrationVersion, string migrationName, DateTime migrationCompletedUtc)
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

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new
		{
			migrationVersion,
			migrationName,
			migrationCompletedUtc
		});
	}

	public void SaveMigrationItemState(MigrationItemDto item)
	{
		throw new NotImplementedException();
	}

	public void SaveUnitTestResult(string relativePath, int crc32Hash, DateTime startedUtc, int executionTimeMs, bool isSuccess, string? errorMessage)
	{
		throw new NotImplementedException();
	}

	public void SetEasyFlowVersion(int version, DateTime migrationDatetime)
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

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new { version, migrationDatetime });
	}

	public void DropDB(bool skipIfNotExists = true)
	{
		throw new NotImplementedException();
	}
}
