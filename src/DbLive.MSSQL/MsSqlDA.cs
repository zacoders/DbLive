using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using System.Data;

namespace DbLive.MSSQL;

public class MsSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	static MsSqlDA()
	{
		SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}
	private SqlConnection GetConnection() => new(_cnn.ConnectionString);

	public async Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync()
	{
		const string query = @"
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
		";

		using var cnn = GetConnection();
		return (await cnn.QueryAsync<MigrationItemDto>(query).ConfigureAwait(false)).ToList();
	}

	public async Task<long?> GetMigrationHashAsync(int version, MigrationItemType itemType)
	{
		const string query = @"
			select content_hash
			from dblive.migration
			where version = @version
			  and item_type = @item_type
		";
		using var cnn = GetConnection();
		return await cnn.QueryFirstOrDefaultAsync<long?>(query, new { version, item_type = itemType.ToString() }).ConfigureAwait(false);
	}

	public async Task<bool> DbLiveInstalledAsync()
	{
		const string query = @"
			select iif(object_id('dblive.version', 'U') is null, 0, 1)
		";

		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<bool>(query).ConfigureAwait(false);
	}

	public async Task<int> GetCurrentMigrationVersionAsync()
	{
		const string query = @"
			select version
			from dblive.dbversion
		";
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<int?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task<int> GetDbLiveVersionAsync()
	{
		const string query = @"
			select version
			from dblive.version
		";
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<int?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task SetDbLiveVersionAsync(int version, DateTime migrationDateTime)
	{
		const string query = @"
			update dblive.version
			set version = @version
			  , applied_utc = @applied_utc;
		";
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(query, new { version, applied_utc = migrationDateTime }).ConfigureAwait(false);
	}

	public async Task SetCurrentMigrationVersionAsync(int version, DateTime migrationCompletedUtc)
	{
		const string query = @"
			update dblive.dbversion
			set version = @version
			  , applied_utc = @applied_utc;
		";
		using var cnn = GetConnection();
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
			using SqlConnection sqlConnection = GetConnection();
			await sqlConnection.OpenAsync().ConfigureAwait(false);
			await SetTransactionIsolationLevelAsync(sqlConnection, isolationLevel).ConfigureAwait(false);
			ServerConnection serverConnection = new(sqlConnection)
			{
				StatementTimeout = GetTimeoutSeconds(timeout)
			};
			_ = serverConnection.ExecuteNonQuery(sqlStatement);
			serverConnection.Disconnect();
		}
		catch (Exception e)
		{
			SqlException? sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, e);
		}
	}

	private static async Task SetTransactionIsolationLevelAsync(SqlConnection cnn, TranIsolationLevel isolationLevel)
	{
		string isolationLevelSql = isolationLevel switch
		{
			TranIsolationLevel.Chaos => "READ UNCOMMITTED;",
			TranIsolationLevel.ReadCommitted => "READ COMMITTED;",
			TranIsolationLevel.RepeatableRead => "REPEATABLE READ;",
			TranIsolationLevel.Serializable => "SERIALIZABLE;",
			TranIsolationLevel.Snapshot => "SNAPSHOT;",
			_ => throw new ArgumentOutOfRangeException(nameof(isolationLevel), $"Unsupported isolation level: {isolationLevel}.")
		};
		_ = await cnn.ExecuteAsync($"SET TRANSACTION ISOLATION LEVEL {isolationLevelSql};").ConfigureAwait(false);
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
			using SqlConnection cnn = GetConnection();

			await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);

			using SqlCommand cmd = cnn.CreateCommand();
			cmd.CommandTimeout = GetTimeoutSeconds(timeout);
			cmd.CommandText = sqlStatement;

			await cnn.OpenAsync().ConfigureAwait(false);

			using SqlDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo).ConfigureAwait(false);

			List<SqlResult> multipleResults = [];
			do
			{
				SqlResult? sqlResult = await ReadResultAsync(reader).ConfigureAwait(false);
				if (sqlResult is not null)
				{
					multipleResults.Add(sqlResult);
				}
			}
			while (await reader.NextResultAsync().ConfigureAwait(false));


			return multipleResults;
		}
		catch (Exception e)
		{
			SqlException? sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, e);
		}
	}

	private static async Task<SqlResult?> ReadResultAsync(SqlDataReader reader)
	{
		DataTable? schemaTable = await reader.GetSchemaTableAsync().ConfigureAwait(false);

		List<SqlColumn> sqlColumns = [];
		if (schemaTable != null)
		{
			foreach (DataRow row in schemaTable.Rows)
			{
				sqlColumns.Add(GetSqlColumn(row));
			}
		}

		if (sqlColumns.Count == 0) return null;

		List<SqlRow> rows = [];
		while (await reader.ReadAsync().ConfigureAwait(false))
		{
			SqlRow row = [];
			for (int i = 0; i < reader.FieldCount; i++)
			{
				row.Add(reader.GetValue(i));
			}
			rows.Add(row);
		}

		return new SqlResult(sqlColumns, rows);
	}

	private static SqlColumn GetSqlColumn(DataRow row)
	{
		return new SqlColumn(
			ColumnName: GetValue<string>(row["ColumnName"]),
			DataType: GetSqlTypeName(
				providerType: GetValue<int>(row["ProviderType"]),
				numericPrecision: GetValueN<short?>(row["NumericPrecision"]),
				numericScale: GetValue<short?>(row["NumericScale"]),
				columnSize: GetValue<int>(row["ColumnSize"])
			)
		);
	}

	private static string GetSqlTypeName(int providerType, short? numericPrecision, short? numericScale, int columnSize)
	{
		SqlDbType dbType = (SqlDbType)providerType;

		return dbType switch
		{
			SqlDbType.BigInt => "bigint",
			SqlDbType.Binary => $"binary({columnSize})",
			SqlDbType.Bit => "bit",
			SqlDbType.Char => $"char({columnSize})",
			SqlDbType.Date => "date",
			SqlDbType.DateTime => "datetime",
			SqlDbType.DateTime2 => $"datetime2({numericScale ?? 7})",
			SqlDbType.DateTimeOffset => $"datetimeoffset({numericScale ?? 7})",
			SqlDbType.Decimal => $"decimal({numericPrecision ?? 18}, {numericScale ?? 0})",
			SqlDbType.Float => "float",
			SqlDbType.Image => "image",
			SqlDbType.Int => "int",
			SqlDbType.Money => "money",
			SqlDbType.NChar => $"nchar({columnSize})",
			SqlDbType.NText => "ntext",
			SqlDbType.NVarChar => columnSize == -1 ? "nvarchar(max)" : $"nvarchar({columnSize})",
			SqlDbType.Real => "real",
			SqlDbType.UniqueIdentifier => "uniqueidentifier",
			SqlDbType.SmallDateTime => "smalldatetime",
			SqlDbType.SmallInt => "smallint",
			SqlDbType.SmallMoney => "smallmoney",
			SqlDbType.Text => "text",
			SqlDbType.Timestamp => "timestamp",
			SqlDbType.TinyInt => "tinyint",
			SqlDbType.VarBinary => columnSize == -1 ? "varbinary(max)" : $"varbinary({columnSize})",
			SqlDbType.VarChar => columnSize == -1 ? "varchar(max)" : $"varchar({columnSize})",
			SqlDbType.Variant => "sql_variant",
			SqlDbType.Xml => "xml",
			_ => "unknown"
		};
	}

	private static T? GetValueN<T>(object objectValue)
	{
		if (objectValue == DBNull.Value) return default;
		return (T?)objectValue;
	}

	private static T GetValue<T>(object objectValue)
	{
		if (objectValue == DBNull.Value) throw new InvalidOperationException("Expected non null value, but NULL received.");
		return (T)objectValue;
	}

	public async Task CreateDBAsync(bool skipIfExists = true)
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string databaseToCreate = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		SqlConnection cnn = new(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		_ = cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)(await cmd.ExecuteScalarAsync().ConfigureAwait(false)) == 1;

		if (dbExists && skipIfExists) return;

		ServerConnection serverCnn = new(cnn);
		_ = serverCnn.ExecuteNonQuery($"create database [{databaseToCreate}];");

		serverCnn.Disconnect();
	}

	public async Task DropDBAsync(bool skipIfNotExists = true)
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string databaseToDrop = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		SqlConnection cnn = new(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		_ = cmd.Parameters.AddWithValue("name", databaseToDrop);
		bool dbExists = (int?)await cmd.ExecuteScalarAsync().ConfigureAwait(false) == 1;

		if (!dbExists)
		{
			if (skipIfNotExists)
			{
				return;
			}
			else
			{
				throw new Exception($"Database '{databaseToDrop}' does not exists.");
			}
		}

		ServerConnection serverCnn = new(cnn);
		_ = serverCnn.ExecuteNonQuery([
			$"alter database [{databaseToDrop}] set single_user with rollback immediate;",
			$"drop database [{databaseToDrop}];"
		]);

		serverCnn.Disconnect();
	}

	public async Task SaveCodeItemAsync(CodeItemDto item)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync("""
			merge into dblive.code as t
			using ( select 1 ) s(c) on t.relative_path = @relative_path
			when matched then update 
				set status = @status
					, content_hash = @content_hash
					, applied_utc = @applied_utc
					, execution_time_ms = @execution_time_ms
					, error = @error
			when not matched then 
				insert (
					relative_path
					, status
					, content_hash
					, applied_utc
					, execution_time_ms
					, created_utc
					, error
				)
				values (
					@relative_path
					, @status
					, @content_hash
					, @applied_utc
					, @execution_time_ms
					, @created_utc
					, @error
				);
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
		using var cnn = GetConnection();
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
		using var cnn = GetConnection();
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
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync("""
			merge into dblive.migration as t
			using ( select 1 ) s(c) on t.version = @version and t.item_type = @item_type
			when matched then update 
			set status = @status
				, content_hash = @content_hash
				, content = @content
				, relative_path = @relative_path
			when not matched then 
			insert (
				version
				, name
				, item_type
				, relative_path
				, status
				, content_hash
				, content
				, created_utc
			)
			values (
				@version
				, @name
				, @item_type
				, @relative_path
				, @status
				, @content_hash
				, @content
				, @created_utc
			);
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
			}
		).ConfigureAwait(false);
	}

	public async Task UpdateMigrationStateAsync(MigrationItemStateDto item)
	{
		using var cnn = GetConnection();
		int? ver = await cnn.QueryFirstOrDefaultAsync<int?>("""
			update dblive.migration
			set status = @status
			  , applied_utc = @applied_utc
			  , execution_time_ms = @execution_time_ms
			  , error = @error
			output inserted.version
			where version = @version
			  and item_type = @item_type
			""",
			new
			{
				version = item.Version,
				item_type = item.ItemType.ToString().ToLower(),
				status = item.Status.ToString().ToLower(),
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				error = item.ErrorMessage
			}
		).ConfigureAwait(false);

		if (ver is null)
		{
			throw new DbLiveMigrationItemMissedSqlException($"Migration item not found. Version: {item.Version}, Type: {item.ItemType}");
		}
	}

	public async Task<bool> MigrationItemExistsAsync(int version, MigrationItemType ItemType)
	{
		using var cnn = GetConnection();
		int? exists = await cnn.QueryFirstOrDefaultAsync<int?>("""
			select 1
			from dblive.migration
			where version = @version
			  and item_type = @item_type
			""",
			new
			{
				version,
				item_type = ItemType.ToString().ToLower()
			}
		).ConfigureAwait(false);
		return exists is not null;
	}

	public async Task<string?> GetMigrationContentAsync(int version, MigrationItemType migrationType)
	{
		using var cnn = GetConnection();
		string? content = await cnn.QueryFirstOrDefaultAsync<string?>("""
			select content
			from dblive.migration
			where version = @version
			  and item_type = @item_type
			""",
			new
			{
				version,
				item_type = migrationType.ToString().ToLower()
			}
		).ConfigureAwait(false);
		return content;
	}

	public async Task SaveUnitTestResultAsync(UnitTestItemDto item)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(
			"""			
			merge into dblive.unit_test_run as t
			using ( select 1 ) s(c) on t.relative_path = @relative_path
			when matched then update 
				set content_hash = @content_hash
				  , run_utc = @run_utc
				  , execution_time_ms = @execution_time_ms
				  , pass = @pass
				  , error = @error
			when not matched then 
				insert (
					relative_path
				  , content_hash
				  , run_utc
				  , execution_time_ms
				  , pass
				  , error
				)
				values (
					@relative_path
				  , @content_hash
				  , @run_utc
				  , @execution_time_ms
				  , @pass
				  , @error
				);
			""",
			new
			{
				relative_path = item.RelativePath,
				content_hash = item.ContentHash,
				run_utc = item.StartedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				pass = item.IsSuccess,
				error = item.ErrorMessage
			}
		).ConfigureAwait(false);
	}

	public async Task MarkItemAsAppliedAsync(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs, long contentHash)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(
			"""
			merge into dblive.folder_items as t
			using ( select 1 ) s(c) on t.folder_type = @folder_type and t.relative_path = @relative_path
			when matched then 
				update set 
					started_utc = @started_utc,
					completed_utc = @completed_utc,
					execution_time_ms = @execution_time_ms,
					content_hash = @content_hash
			when not matched then 
				insert (
					  folder_type
					, relative_path
					, created_utc
					, started_utc
					, completed_utc
					, execution_time_ms
					, content_hash
				)
				values (
					  @folder_type
					, @relative_path
					, sysutcdatetime()
					, @started_utc
					, @completed_utc
					, @execution_time_ms
					, @content_hash
				);
			""",
			new
			{
				folder_type = projectFolder.ToString(),
				relative_path = relativePath,
				started_utc = startedUtc,
				completed_utc = completedUtc,
				execution_time_ms = executionTimeMs,
				content_hash = contentHash
			}
		).ConfigureAwait(false);
	}
}