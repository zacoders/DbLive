using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using System.Data;

namespace DbLive.PostgreSQL;

public class PostgreSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	public async Task CreateDBAsync(bool skipIfExists = true)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(_cnn.ConnectionString);

		string? databaseToCreate = cnnBuilder.Database ?? throw new ArgumentException("Database must be specified in connection string.");

		cnnBuilder.Database = "postgres";

		var cnn = new NpgsqlConnection(cnnBuilder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		NpgsqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from pg_catalog.pg_database where lower(datname) = lower(:name)";
		_ = cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)await cmd.ExecuteScalarAsync().ConfigureAwait(false) == 1;

		if (dbExists && skipIfExists) return;

		_ = cnn.Execute($"""create database "{databaseToCreate}" """);
		await cnn.CloseAsync().ConfigureAwait(false);
	}

	public async Task<bool> DbLiveInstalledAsync()
	{
		const string query = @"
			select exists ( select * from pg_tables where schemaname = 'DbLive' and tablename = 'Migrations'
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return await cnn.ExecuteScalarAsync<bool>(query).ConfigureAwait(false);
	}

	public async Task ExecuteNonQueryAsync(
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
			await cnn.OpenAsync().ConfigureAwait(false);
			// todo: apply transaction isolation level
			NpgsqlCommand cmd = cnn.CreateCommand();
			cmd.CommandTimeout = timeoutSeconds;
			cmd.CommandText = sqlStatement;
			_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (Exception e)
		{
			PostgresException? pgException = e.Get<PostgresException>();
			throw new DbLiveSqlException(pgException?.Message ?? e.Message, e);
		}
	}

	public async Task<int> GetDbLiveVersionAsync()
	{
		const string query = @"
			select version
			from dblive.version
		";

		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return await cnn.ExecuteScalarAsync<int?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync()
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
		return (await cnn.QueryAsync<MigrationItemDto>(query).ConfigureAwait(false)).ToList();
	}

	public async Task<CodeItemDto?> FindCodeItemAsync(string relativePath)
	{
		using var cnn = new NpgsqlConnection(_cnn.ConnectionString);
		return await cnn.QueryFirstOrDefaultAsync<CodeItemDto>(
			"dblive.get_code_item",
			new { relative_path = relativePath },
			commandType: CommandType.StoredProcedure
		).ConfigureAwait(false);
	}

	public async Task SetCurrentMigrationVersionAsync(int migrationVersion, DateTime migrationCompletedUtc)
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
		_ = await cnn.ExecuteAsync(query, new
		{
			migrationVersion,
			migrationCompletedUtc
		}).ConfigureAwait(false);
	}

	public async Task SetDbLiveVersionAsync(int version, DateTime migrationDateTime)
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
		_ = await cnn.ExecuteAsync(query, new { version, migrationDateTime }).ConfigureAwait(false);
	}

	public Task<int?> GetMigrationHashAsync(int version, MigrationItemType itemType) => throw new NotImplementedException();
	public Task<int> GetCurrentMigrationVersionAsync() => throw new NotImplementedException();
	public Task SaveCodeItemAsync(CodeItemDto item) => throw new NotImplementedException();
	public Task MarkCodeAsVerifiedAsync(string relativePath, DateTime verifiedUtc) => throw new NotImplementedException();
	public Task DropDBAsync(bool skipIfNotExists = true) => throw new NotImplementedException();
	public Task<List<SqlResult>> ExecuteQueryMultipleAsync(string sqlStatement, TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted, TimeSpan? timeout = null) => throw new NotImplementedException();
	public Task SaveMigrationItemAsync(MigrationItemSaveDto item) => throw new NotImplementedException();
	public Task UpdateMigrationStateAsync(MigrationItemStateDto item) => throw new NotImplementedException();
	public Task<bool> MigrationItemExistsAsync(int version, MigrationItemType itemType) => throw new NotImplementedException();
	public Task<string?> GetMigrationContentAsync(int version, MigrationItemType undo) => throw new NotImplementedException();
	public Task SaveUnitTestResultAsync(UnitTestItemDto item) => throw new NotImplementedException();
	public Task MarkItemAsAppliedAsync(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs) => throw new NotImplementedException();
}
