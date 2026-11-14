using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using DbLive.Schema;
using System.Data;

namespace DbLive.MSSQL;

public class MsSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	public DbProvider Provider => DbProvider.MsSql;

	public async Task<bool> IsTransactionUsableAsync()
	{
		// a transaction rolled back by the server does not fail the next commands of the scope: they run in autocommit mode.
		// Transaction.Current stays Active then, only the server knows.
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<int>("select @@trancount").ConfigureAwait(false) > 0;
	}

	// no savepoint: an error that does not roll back the whole transaction leaves it usable, and after one that does
	// (XACT_ABORT ON, a failed conversion, a deadlock) there is no savepoint to return to.
	public Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	) => ExecuteNonQueryAsync(sqlStatement, isolationLevel, timeout);

	internal const int ErrorColumnLength = 4000;
	internal const string TruncatedMark = "... [truncated]";

	/// <summary>
	/// The column <c>error</c> of the dblive tables is <c>nvarchar(4000)</c>: a longer text would fail the write of the row, and that error
	/// would replace the one being recorded. The text is cut to fit; the full error is in the exception and in the log.
	/// </summary>
	internal static string? FitErrorColumn(string? error)
	{
		if (error is null || error.Length <= ErrorColumnLength)
		{
			return error;
		}

		int length = ErrorColumnLength - TruncatedMark.Length;

		// do not split a surrogate pair.
		if (char.IsHighSurrogate(error[length - 1]))
		{
			length--;
		}

		return string.Concat(error.AsSpan(0, length), TruncatedMark);
	}

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
		return (await cnn.QueryAsync<MigrationItemRow>(query).ConfigureAwait(false)).Select(r => r.ToDto()).ToList();
	}

	public async Task<bool> DbLiveInstalledAsync()
	{
		const string query = @"
			select iif(object_id('dblive.version', 'U') is null, 0, 1)
		";

		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<bool>(query).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<long>> GetAppliedMigrationVersionsAsync()
	{
		const string query = @"
			select version
			from dblive.migration
			where item_type = 'migration'
			  and status = 'applied'
		";
		using var cnn = GetConnection();
		return (await cnn.QueryAsync<long>(query).ConfigureAwait(false)).ToList();
	}

	public async Task<long> GetSelfMigrationVersionAsync()
	{
		const string query = @"
			select version
			from dblive.version
		";
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<long?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task SetSelfMigrationVersionAsync(long version, DateTime migrationDateTime)
	{
		const string query = @"
			update dblive.version
			set version = @version
			  , applied_utc = @applied_utc;
		";
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(query, new { version, applied_utc = migrationDateTime }).ConfigureAwait(false);
	}

	// the column comes with self-migration 12: a database deployed before it has none, and a batch that names
	// a missing column does not compile. Without the column the batch returns no result set, and the value is null.
	public async Task<Version?> GetMinDbLiveLibVersionAsync()
	{
		const string query = @"
			if col_length('dblive.version', 'min_dblive_lib_version') is not null
				exec sys.sp_executesql N'select min_dblive_lib_version from dblive.version';
		";
		using var cnn = GetConnection();
		string? version = await cnn.ExecuteScalarAsync<string?>(query).ConfigureAwait(false);
		return version is null ? null : Version.Parse(version);
	}

	public async Task SetMinDbLiveLibVersionAsync(Version version)
	{
		const string query = @"
			update dblive.version
			set min_dblive_lib_version = @min_dblive_lib_version;
		";
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(query, new { min_dblive_lib_version = version.ToString() }).ConfigureAwait(false);
	}

	// the column comes with self-migration 6, and the ProjectId is checked before the self-deployment: a database deployed
	// before it has none. Without the column the batch returns no result set, and the value is null.
	public async Task<string?> GetProjectIdAsync()
	{
		const string query = @"
			if col_length('dblive.version', 'project_id') is not null
				exec sys.sp_executesql N'select project_id from dblive.version';
		";
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<string?>(query).ConfigureAwait(false);
	}

	public async Task SetProjectIdAsync(string projectId)
	{
		const string query = @"
			update dblive.version
			set project_id = @project_id;
		";
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(query, new { project_id = ProjectIdTools.Normalize(projectId) }).ConfigureAwait(false);
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
			if (Transaction.Current is null)
			{
				await SetTransactionIsolationLevelAsync(sqlConnection, isolationLevel).ConfigureAwait(false);
			}
			ServerConnection serverConnection = new(sqlConnection)
			{
				StatementTimeout = GetTimeoutSeconds(timeout)
			};
			_ = serverConnection.ExecuteNonQuery(sqlStatement);
			serverConnection.Disconnect();
		}
		catch (Exception e)
		{
			// todo: We are using Microsoft.SqlServer.Management.Common.ServerConnection
			//       It does not provide global error line number, just line number in the 'GO' batch
			//       Consider splitting statements manually.
			SqlException? sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, GetSqlServerError(e), e);
		}
	}

	private static async Task SetTransactionIsolationLevelAsync(SqlConnection cnn, TranIsolationLevel isolationLevel)
	{
		string isolationLevelSql = TranIsolationLevelMapper.ToSql(isolationLevel, DbProvider.MsSql);
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

			await cnn.OpenAsync().ConfigureAwait(false);
			if (Transaction.Current is null)
			{
				await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);
			}

			using SqlCommand cmd = cnn.CreateCommand();
			cmd.CommandTimeout = GetTimeoutSeconds(timeout);
			cmd.CommandText = sqlStatement;

			// no CommandBehavior.KeyInfo: it turns on browse mode, and SQL Server adds the key columns of the base table as hidden columns.
			using SqlDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

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
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, GetSqlServerError(e), e);
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

		using SqlConnection cnn = new(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		_ = cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)(await cmd.ExecuteScalarAsync().ConfigureAwait(false)) == 1;

		if (dbExists && skipIfExists) return;

		ServerConnection serverCnn = new(cnn);
		_ = serverCnn.ExecuteNonQuery($"create database [{databaseToCreate}];");

		serverCnn.Disconnect();
	}

	// Cannot open database "..." requested by the login. The login failed.
	private const int CannotOpenDatabaseError = 4060;

	public async Task CheckDatabaseExistsAsync()
	{
		// without retries: SqlClient retries error 4060 for 10 seconds. Any other outcome is left to the next connection,
		// which retries transient errors (a database on Azure SQL coming online) by the settings of the connection string.
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString) { ConnectRetryCount = 0 };

		try
		{
			using SqlConnection cnn = new(builder.ConnectionString);
			await cnn.OpenAsync().ConfigureAwait(false);
		}
		catch (SqlException e) when (e.Number == CannotOpenDatabaseError)
		{
			// a login without access to an existing database gets the same error: master tells the two apart.
			if (await DatabaseIsMissingAsync(builder).ConfigureAwait(false))
			{
				throw new DatabaseNotFoundException(builder.InitialCatalog, builder.DataSource, e);
			}
		}
		catch (SqlException)
		{
			// not a missing database: the next connection fails with this error, or succeeds after a retry.
		}
	}

	/// <summary>
	/// True only when master shows that the database does not exist. False when it exists, and when master cannot tell:
	/// the login cannot connect to master, or sees only its own databases (no VIEW ANY DATABASE permission).
	/// </summary>
	private static async Task<bool> DatabaseIsMissingAsync(SqlConnectionStringBuilder builder)
	{
		const string query = @"
			select case
				when exists (select 1 from sys.databases where name = @name) then 0
				when has_perms_by_name(null, null, 'VIEW ANY DATABASE') = 1 then 1
				else 0
			end
		";

		SqlConnectionStringBuilder masterBuilder = new(builder.ConnectionString) { InitialCatalog = "master" };

		try
		{
			using SqlConnection cnn = new(masterBuilder.ConnectionString);
			return await cnn.ExecuteScalarAsync<bool>(query, new { name = builder.InitialCatalog }).ConfigureAwait(false);
		}
		catch (SqlException)
		{
			return false;
		}
	}

	public async Task DropDbAsync(bool skipIfNotExists = true)
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string databaseToDrop = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		using SqlConnection cnn = new(builder.ConnectionString);
		await cnn.OpenAsync().ConfigureAwait(false);

		using SqlCommand cmd = cnn.CreateCommand();
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

	// relative_path is stored in lower case (ToStoredPath), so a file is identified ignoring case, the same way it is found in the project.
	public async Task SaveCodeItemAsync(CodeItemDto item)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync("""
			merge into dblive.code as t
			using ( select 1 ) s(c) on t.relative_path = @relative_path
			when matched then update
				set item_type = @item_type
					, status = @status
					, content_hash = @content_hash
					, applied_utc = @applied_utc
					, execution_time_ms = @execution_time_ms
					, error = @error
			when not matched then
				insert (
					relative_path
					, item_type
					, status
					, content_hash
					, applied_utc
					, execution_time_ms
					, created_utc
					, error
				)
				values (
					@relative_path
					, @item_type
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
				relative_path = item.RelativePath.ToStoredPath(),
				item_type = item.ItemType.ToString().ToLowerInvariant(),
				status = item.Status.ToString().ToLowerInvariant(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc,
				error = FitErrorColumn(item.ErrorMessage)
			}
		).ConfigureAwait(false);
	}

	/// <summary>
	/// The paths as one table-valued parameter (self-migration 13), whatever their number: a command takes at most 2100 parameters.
	/// The paths of the project are unique, also in lower case: two paths that differ only by case stop the deployment.
	/// </summary>
	private static SqlMapper.ICustomQueryParameter ToPathList(IEnumerable<string> relativePaths)
	{
		DataTable table = new();
		_ = table.Columns.Add("value", typeof(string));

		foreach (string relativePath in relativePaths)
		{
			_ = table.Rows.Add(relativePath.ToStoredPath());
		}

		return table.AsTableValuedParameter("dblive.tvp_unique_string_list");
	}

	public async Task MarkCodeAsVerifiedAsync(IReadOnlyCollection<string> relativePaths, DateTime verifiedUtc)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(
			"""
			update c
			set verified_utc = @verified_utc
			from dblive.code as c
			join @paths as p on p.value = c.relative_path;
			""",
			new { paths = ToPathList(relativePaths), verified_utc = verifiedUtc }
		).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<string>> MarkMissingCodeAsRemovedAsync(IReadOnlyCollection<string> projectRelativePaths, DateTime verifiedUtc)
	{
		using var cnn = GetConnection();
		return (await cnn.QueryAsync<string>(
			"""
			update c
			set status = 'removed'
			  , verified_utc = @verified_utc
			output inserted.relative_path
			from dblive.code as c
			where c.status <> 'removed'
			  and not exists ( select 1 from @paths as p where p.value = c.relative_path );
			""",
			new { paths = ToPathList(projectRelativePaths), verified_utc = verifiedUtc }
		).ConfigureAwait(false)).ToList();
	}

	public async Task<IReadOnlyCollection<CodeItemDto>> GetCodeItemsAsync()
	{
		using var cnn = GetConnection();
		return (await cnn.QueryAsync<CodeItemDto>(
			"""
			select relative_path
				 , item_type
				 , content_hash
				 , applied_utc
				 , created_utc
				 , execution_time_ms
				 , verified_utc
				 , error as error_message
				 , status
			from dblive.code
			"""
		).ConfigureAwait(false)).ToList();
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
				, relative_path = @relative_path
			when not matched then
			insert (
				version
				, name
				, item_type
				, relative_path
				, status
				, content_hash
				, created_utc
			)
			values (
				@version
				, @name
				, @item_type
				, @relative_path
				, @status
				, @content_hash
				, @created_utc
			);
			""",
			new
			{
				version = item.Version,
				name = item.Name,
				item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
				relative_path = item.RelativePath.ToStoredPath(),
				content_hash = item.ContentHash,
				status = item.Status.ToString().ToLowerInvariant(),
				created_utc = item.CreatedUtc
			}
		).ConfigureAwait(false);
	}

	public async Task RepairMigrationChecksumAsync(MigrationChecksumRepairDto item)
	{
		using var cnn = GetConnection();
		int rowsAffected = await cnn.ExecuteAsync("""
			update dblive.migration
			set content_hash = @content_hash
			  , relative_path = @relative_path
			where version = @version
			  and item_type = @item_type
			  and status = 'applied'
			""",
			new
			{
				version = item.Version,
				item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
				content_hash = item.ContentHash,
				relative_path = item.RelativePath.ToStoredPath()
			}
		).ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			throw new DbLiveMigrationItemMissedSqlException(
				$"Applied migration item not found for checksum repair. Version: {item.Version}, Type: {item.ItemType}");
		}
	}

	public async Task UpdateMigrationStateAsync(MigrationItemStateDto item)
	{
		using var cnn = GetConnection();
		long? ver = await cnn.QueryFirstOrDefaultAsync<long?>("""
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
				item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
				status = item.Status.ToString().ToLowerInvariant(),
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				error = FitErrorColumn(item.ErrorMessage)
			}
		).ConfigureAwait(false);

		if (ver is null)
		{
			throw new DbLiveMigrationItemMissedSqlException($"Migration item not found. Version: {item.Version}, Type: {item.ItemType}");
		}
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
				relative_path = item.RelativePath.ToStoredPath(),
				content_hash = item.ContentHash,
				run_utc = item.StartedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				pass = item.IsSuccess,
				error = FitErrorColumn(item.ErrorMessage)
			}
		).ConfigureAwait(false);
	}

	public async Task<SchemaSnapshot> GetSchemaSnapshotAsync()
	{
		using var cnn = GetConnection();
		IEnumerable<SchemaObjectRow> rows = await cnn.QueryAsync<SchemaObjectRow>(MsSqlSchemaSnapshot.Query).ConfigureAwait(false);
		return new SchemaSnapshot(rows.Select(r => MsSqlSchemaSnapshot.Normalize(r.ToSchemaObject())));
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
				relative_path = relativePath.ToStoredPath(),
				started_utc = startedUtc,
				completed_utc = completedUtc,
				execution_time_ms = executionTimeMs,
				content_hash = contentHash
			}
		).ConfigureAwait(false);
	}

	private static string GetSqlServerError(Exception ex)
	{
		SqlException? sqlEx = FindSqlException(ex);

		if (sqlEx == null)
			return "";

		var sb = new System.Text.StringBuilder();

		_ = sb.AppendLine("SQL Server error:");

		foreach (SqlError err in sqlEx.Errors)
		{
			_ = sb.AppendLine($"""
			Msg {err.Number}, Level {err.Class}, State {err.State}, Line {err.LineNumber}
			{err.Message}
			""");

			if (!string.IsNullOrEmpty(err.Procedure))
			{
				_ = sb.AppendLine($"Procedure: {err.Procedure}");
			}
		}

		return sb.ToString();
	}

	private static SqlException? FindSqlException(Exception ex)
	{
		Exception? cur = ex;

		while (cur != null)
		{
			if (cur is SqlException sql)
				return sql;

			cur = cur.InnerException;
		}

		return null;
	}
}