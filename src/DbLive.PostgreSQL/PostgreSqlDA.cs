using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using System.Data;

namespace DbLive.PostgreSQL;

public class PostgreSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
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

	public bool DbLiveInstalled()
	{
		const string query = @"
			select exists ( select * from pg_tables where schemaname = 'DbLive' and tablename = 'Migrations'
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public void ExecuteNonQuery(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		try
		{
			int timeoutSeconds = 30;
			if (timeout.HasValue)
			{
				timeoutSeconds = (int)timeout.Value.TotalSeconds;
			}
			using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
			cnn.Open();
			// todo: apply transaction isolation level
			var cmd = cnn.CreateCommand();
			cmd.CommandTimeout = timeoutSeconds;
			cmd.CommandText = sqlStatement;
			cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			var pgException = e.Get<PostgresException>();
			throw new DbLiveSqlException(pgException?.Message ?? e.Message, e);
		}
	}

	public List<SqlResult> ExecuteQueryMultiple(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		throw new NotImplementedException();
	}

	public int GetDbLiveVersion()
	{
		const string query = @"
			select version
			from dblive.version
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public IReadOnlyCollection<MigrationItemDto> GetMigrations()
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
			from dblive.migration
		";
		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationItemDto>(query).ToList();
	}

	public CodeItemDto? FindCodeItem(string relativePath)
	{
		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return cnn.QueryFirstOrDefault<CodeItemDto>(
			"dblive.get_code_item",
			new { relative_path = relativePath },
			commandType: CommandType.StoredProcedure
		);
	}

	public void SaveCodeItem(CodeItemDto item) => throw new NotImplementedException();

	public void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc)
	{
		throw new NotImplementedException();
	}

	public void SetCurrentMigrationVersion(int migrationVersion, DateTime migrationCompletedUtc)
	{
		//todo: refactor table name and column names for postgres.
		string query = @"
			insert into dblive.migration
			(
				migrationversion
			  , migrationstarted
			  , migrationcompleted
			)
			values (
				@migrationversion
			  , @migrationstartedutc
			  , @migrationcompletedutc
			)
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new
		{
			migrationVersion,
			migrationCompletedUtc
		});
	}

	public void SaveMigrationItemState(MigrationItemDto item)
	{
		throw new NotImplementedException();
	}

	public void SaveUnitTestResult(UnitTestItemDto item)
	{
		throw new NotImplementedException();
	}

	public void SetDbLiveVersion(int version, DateTime migrationDateTime)
	{
		const string query = @"
			merge into dblive.version as t
			using ( select 1 ) as s(c) on 1 = 1
			when not matched then 
				insert ( version, migrationdatetime ) values ( @version, @migrationDatetime )
			when matched then update
				set version = @version
			      , migrationDatetime = migrationdatetime;
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new { version, migrationDateTime });
	}

	public void DropDB(bool skipIfNotExists = true)
	{
		throw new NotImplementedException();
	}

	public void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs)
	{
		throw new NotImplementedException();
	}

	public int GetCurrentMigrationVersion() => throw new NotImplementedException();
	public int? GetMigrationHash(int version, MigrationItemType itemType) => throw new NotImplementedException();
	public void SaveMigrationItem(MigrationItemSaveDto item) => throw new NotImplementedException();
	public void UpdateMigrationState(MigrationItemStateDto item) => throw new NotImplementedException();
	public string? GetMigrationContent(int version, MigrationItemType undo) => throw new NotImplementedException();
}
