using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using System.Data;

namespace DbLive.PostgreSQL;

public class PostgreSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	static PostgreSqlDA()
	{
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	private NpgsqlConnection CreateConnection() => new(_cnn.ConnectionString);

	public async Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync()
	{
		const string query = """
			select version
			     , name
			     , item_type
			     , relative_path
			     , status
			     , content_hash
			     , created_utc
			     , applied_utc
			     , execution_time_ms
			from dblive.migration
		""";

		using NpgsqlConnection cnn = CreateConnection();
		return (await cnn.QueryAsync<MigrationItemDto>(query).ConfigureAwait(false)).ToList();
	}

	public async Task<long?> GetMigrationHashAsync(int version, MigrationItemType itemType)
	{
		const string query = """
			select content_hash
			from dblive.migration
			where version = @version
			  and item_type = @item_type
		""";

		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.QueryFirstOrDefaultAsync<long?>(query, new
		{
			version,
			item_type = itemType.ToString().ToLower()
		}).ConfigureAwait(false);
	}

	public async Task<bool> DbLiveInstalledAsync()
	{
		const string query = """
			select 1
			from information_schema.tables
			where table_schema = 'dblive'
			  and table_name = 'version'
		""";

		using NpgsqlConnection cnn = CreateConnection();
		int? exists = await cnn.QueryFirstOrDefaultAsync<int?>(query).ConfigureAwait(false);
		return exists is not null;
	}

	public async Task<int> GetCurrentMigrationVersionAsync()
	{
		const string query = "select version from dblive.dbversion;";
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.ExecuteScalarAsync<int?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task<int> GetDbLiveVersionAsync()
	{
		const string query = "select version from dblive.version;";
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.ExecuteScalarAsync<int?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task SetDbLiveVersionAsync(int version, DateTime migrationDateTime)
	{
		const string query = """
			update dblive.version
			set version = @version,
			    applied_utc = @applied_utc;
		""";

		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(query, new
		{
			version,
			applied_utc = migrationDateTime
		}).ConfigureAwait(false);
	}

	public async Task SetCurrentMigrationVersionAsync(int version, DateTime migrationCompletedUtc)
	{
		const string query = """
			update dblive.dbversion
			set version = @version,
			    applied_utc = @applied_utc;
		""";

		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(query, new
		{
			version,
			applied_utc = migrationCompletedUtc
		}).ConfigureAwait(false);
	}

	public async Task ExecuteNonQueryAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		try
		{
			using NpgsqlConnection cnn = CreateConnection();
			await cnn.OpenAsync().ConfigureAwait(false);

			await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);

			using NpgsqlCommand cmd = new(sqlStatement, cnn);
			cmd.CommandTimeout = GetTimeoutSeconds(timeout);

			_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
		catch (Exception e)
		{
			throw new DbLiveSqlException(e.Message, e);
		}
	}

	private static async Task SetTransactionIsolationLevelAsync(NpgsqlConnection cnn, TranIsolationLevel isolationLevel)
	{
		string isolationLevelSql = isolationLevel switch
		{
			TranIsolationLevel.ReadCommitted => "read committed",
			TranIsolationLevel.RepeatableRead => "repeatable read",
			TranIsolationLevel.Serializable => "serializable",
			_ => "read committed"
		};

		using NpgsqlCommand cmd = new($"set transaction isolation level {isolationLevelSql};", cnn);
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private static int GetTimeoutSeconds(TimeSpan? timeout)
	{
		return timeout.HasValue ? (int)timeout.Value.TotalSeconds : 30;
	}

	public async Task<List<SqlResult>> ExecuteQueryMultipleAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		try
		{
			using NpgsqlConnection cnn = CreateConnection();
			await cnn.OpenAsync().ConfigureAwait(false);

			await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);

			using NpgsqlCommand cmd = new(sqlStatement, cnn);
			cmd.CommandTimeout = GetTimeoutSeconds(timeout);

			using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

			List<SqlResult> results = [];

			do
			{
				SqlResult? r = await ReadResultAsync(reader).ConfigureAwait(false);
				if (r is not null)
				{
					results.Add(r);
				}
			}
			while (await reader.NextResultAsync().ConfigureAwait(false));

			return results;
		}
		catch (Exception e)
		{
			throw new DbLiveSqlException(e.Message, e);
		}
	}

	private static async Task<SqlResult?> ReadResultAsync(NpgsqlDataReader reader)
	{
		if (!reader.HasRows && reader.FieldCount == 0) return null;

		var columns = Enumerable.Range(0, reader.FieldCount)
			.Select(i => new SqlColumn(
				ColumnName: reader.GetName(i),
				DataType: reader.GetDataTypeName(i).ToLowerInvariant()
			)).ToList();

		List<SqlRow> rows = [];
		while (await reader.ReadAsync().ConfigureAwait(false))
		{
			SqlRow r = [];
			for (int i = 0; i < reader.FieldCount; i++)
			{
				r.Add(reader.GetValue(i));
			}
			rows.Add(r);
		}

		return new SqlResult(columns, rows);
	}


	public async Task CreateDBAsync(bool skipIfExists = true)
	{
		var builder = new NpgsqlConnectionStringBuilder(_cnn.ConnectionString);
		string? dbName = builder.Database;
		builder.Database = "postgres"; // Connect to maintenance DB

		using var cnn = new NpgsqlConnection(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		bool exists = await cnn.ExecuteScalarAsync<bool>(
			"select exists(select 1 from pg_database where datname = @name);",
			new { name = dbName }
		).ConfigureAwait(false);

		if (exists && skipIfExists) return;

		// Use a command builder to safely quote the identifier
		using var cmd = cnn.CreateCommand();
		var quotedDbName = new NpgsqlCommandBuilder().QuoteIdentifier(dbName!);
		cmd.CommandText = $"CREATE DATABASE {quotedDbName};";
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	public async Task DropDBAsync(bool skipIfNotExists = true)
	{
		NpgsqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string? dbName = builder.Database;
		builder.Database = "postgres";

		using NpgsqlConnection cnn = new(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		bool exists = await cnn.ExecuteScalarAsync<bool>(
			"select exists(select 1 from pg_database where datname = @name);",
			new { name = dbName }
		).ConfigureAwait(false);

		if (!exists)
		{
			if (skipIfNotExists) return;
			throw new Exception($"Database '{dbName}' does not exist.");
		}

		_ = await cnn.ExecuteAsync($"""
			revoke connect on database "{dbName}" from public;
			select pg_terminate_backend(pid) from pg_stat_activity where datname = '{dbName}';
			drop database "{dbName}";
		""").ConfigureAwait(false);
	}


	public async Task SaveCodeItemAsync(CodeItemDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
				insert into dblive.code (
					relative_path, status, content_hash, applied_utc,
					execution_time_ms, created_utc, error
				)
				values (
					@relative_path, @status, @content_hash, @applied_utc,
					@execution_time_ms, @created_utc, @error
				)
				on conflict (relative_path) do update set
					status = excluded.status,
					content_hash = excluded.content_hash,
					applied_utc = excluded.applied_utc,
					execution_time_ms = excluded.execution_time_ms,
					error = excluded.error;
			""",
			new
			{
				relative_path = item.RelativePath,
				status = item.Status.ToString().ToLower(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc,
				error = item.ErrorMessage
			}
		).ConfigureAwait(false);
	}

	public async Task MarkCodeAsVerifiedAsync(string relativePath, DateTime verifiedUtc)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(
			"""
			update dblive.code
			set verified_utc = @verified_utc
			where relative_path = @relative_path
			""",
			new { relative_path = relativePath, verified_utc = verifiedUtc }
		).ConfigureAwait(false);
	}
	public async Task<CodeItemDto?> FindCodeItemAsync(string relativePath)
	{
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.QueryFirstOrDefaultAsync<CodeItemDto>(
			"""
			select relative_path
				 , content_hash
				 , applied_utc
				 , created_utc
				 , execution_time_ms
				 , verified_utc
				 , error as error_message
				 , status
			from dblive.code
			where relative_path = @relative_path
			""",
			new { relative_path = relativePath }
		).ConfigureAwait(false);
	}
	public async Task SaveMigrationItemAsync(MigrationItemSaveDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
			insert into dblive.migration (
				version, name, item_type, relative_path,
				status, content_hash, content, created_utc
			)
			values (
				@version, @name, @item_type, @relative_path,
				@status, @content_hash, @content, @created_utc
			)
			on conflict (version, item_type) do update set
				status = excluded.status,
				content_hash = excluded.content_hash,
				content = excluded.content,
				relative_path = excluded.relative_path;
			""",
		new
		{
			version = item.Version,
			name = item.Name,
			item_type = item.ItemType.ToString().ToLower(),
			relative_path = item.RelativePath,
			content_hash = item.ContentHash,
			content = item.Content,
			status = item.Status.ToString().ToLower(),
			created_utc = item.CreatedUtc
		}).ConfigureAwait(false);
	}
	public async Task UpdateMigrationStateAsync(MigrationItemStateDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();

		int? ver = await cnn.QueryFirstOrDefaultAsync<int?>("""
			update dblive.migration
			set status = @status,
				applied_utc = @applied_utc,
				execution_time_ms = @execution_time_ms,
				error = @error
			where version = @version
			  and item_type = @item_type
			returning version;
			""",
		new
		{
			version = item.Version,
			item_type = item.ItemType.ToString().ToLower(),
			status = item.Status.ToString().ToLower(),
			applied_utc = item.AppliedUtc,
			execution_time_ms = item.ExecutionTimeMs,
			error = item.ErrorMessage
		}).ConfigureAwait(false);

		if (ver is null)
		{
			throw new DbLiveMigrationItemMissedSqlException(
				$"Migration item not found. Version: {item.Version}, Type: {item.ItemType}"
			);
		}
	}
	public async Task<bool> MigrationItemExistsAsync(int version, MigrationItemType itemType)
	{
		using NpgsqlConnection cnn = CreateConnection();
		int? exists = await cnn.QueryFirstOrDefaultAsync<int?>("""
			select 1
			from dblive.migration
			where version = @version
			  and item_type = @item_type
			""",
		new
		{
			version,
			item_type = itemType.ToString().ToLower()
		}).ConfigureAwait(false);

		return exists is not null;
	}
	public async Task<string?> GetMigrationContentAsync(int version, MigrationItemType migrationType)
	{
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.QueryFirstOrDefaultAsync<string?>("""
			select content
			from dblive.migration
			where version = @version
			  and item_type = @item_type
			""",
		new
		{
			version,
			item_type = migrationType.ToString().ToLower()
		}).ConfigureAwait(false);
	}
	public async Task SaveUnitTestResultAsync(UnitTestItemDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
			insert into dblive.unit_test_run (
				relative_path, content_hash, run_utc,
				execution_time_ms, pass, error
			)
			values (
				@relative_path, @content_hash, @run_utc,
				@execution_time_ms, @pass, @error
			)
			on conflict (relative_path) do update set
				content_hash = excluded.content_hash,
				run_utc = excluded.run_utc,
				execution_time_ms = excluded.execution_time_ms,
				pass = excluded.pass,
				error = excluded.error;
			""",
		new
		{
			relative_path = item.RelativePath,
			content_hash = item.ContentHash,
			run_utc = item.StartedUtc,
			execution_time_ms = item.ExecutionTimeMs,
			pass = item.IsSuccess,
			error = item.ErrorMessage
		}).ConfigureAwait(false);
	}
	public async Task MarkItemAsAppliedAsync(
		ProjectFolder projectFolder,
		string relativePath,
		DateTime startedUtc,
		DateTime completedUtc,
		long executionTimeMs,
		long contentHash
	)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
			insert into dblive.folder_items (
				folder_type, relative_path, created_utc,
				started_utc, completed_utc, execution_time_ms, content_hash
			)
			values (
				@folder_type, @relative_path, now(),
				@started_utc, @completed_utc, @execution_time_ms, @content_hash
			)
			on conflict (folder_type, relative_path) do update set
				started_utc = excluded.started_utc,
				completed_utc = excluded.completed_utc,
				execution_time_ms = excluded.execution_time_ms,
				content_hash = excluded.content_hash;
			""",
		new
		{
			folder_type = projectFolder.ToString(),
			relative_path = relativePath,
			started_utc = startedUtc,
			completed_utc = completedUtc,
			execution_time_ms = executionTimeMs,
			content_hash = contentHash
		}).ConfigureAwait(false);
	}

}
