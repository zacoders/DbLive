using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using DbLive.Schema;
using Npgsql.Schema;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;

namespace DbLive.PostgreSQL;

internal class PostgreSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	public DbProvider Provider => DbProvider.PostgreSql;

	// an error aborts the transaction: every later statement fails with 25P02 until the rollback to a savepoint or of the transaction.
	public async Task<bool> IsTransactionUsableAsync()
	{
		using NpgsqlConnection cnn = CreateConnection();
		try
		{
			_ = await cnn.ExecuteScalarAsync<int>("select 1").ConfigureAwait(false);
			return true;
		}
		catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InFailedSqlTransaction)
		{
			return false;
		}
	}

	// a file is one command, which runs as one implicit transaction whatever DbLive does.
	public bool RunsCodeItemWithoutTransaction(string sqlStatement) => false;

	// not a name a user script would take: a script that sets a savepoint of the same name would hide this one.
	// One name for all files: code in the deployment transaction runs on one thread, and each file releases or rolls back its savepoint.
	private const string CodeItemSavepoint = "dblive_code_item";

	// in the deployment transaction the file runs under a savepoint: the rollback to it ends the aborted state and undoes the statements
	// of the file. Each savepoint is a subtransaction.
	public async Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	)
	{
		if (System.Transactions.Transaction.Current is null)
		{
			await ExecuteNonQueryAsync(sqlStatement, isolationLevel, timeout).ConfigureAwait(false);
			return;
		}

		await ExecuteInTransactionAsync($"savepoint {CodeItemSavepoint};").ConfigureAwait(false);
		
		try
		{
			await ExecuteNonQueryAsync(sqlStatement, isolationLevel, timeout).ConfigureAwait(false);
		}
		catch
		{
			await TryRollbackToSavepointAsync().ConfigureAwait(false);
			throw;
		}

		await ExecuteInTransactionAsync($"release savepoint {CodeItemSavepoint};").ConfigureAwait(false);
	}

	// fails when the error of the file broke the connection or the transaction timed out. The error of the file is the one to throw,
	// and IsTransactionUsableAsync tells the caller that the transaction is gone.
	private async Task TryRollbackToSavepointAsync()
	{
		try
		{
			await ExecuteInTransactionAsync($"rollback to savepoint {CodeItemSavepoint};").ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
	}

	// separate commands, not text added to the script: a script that ends with a '--' comment would swallow an added statement.
	// Every connection opened in the scope gets the session of the transaction.
	private async Task ExecuteInTransactionAsync(string sql)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(sql).ConfigureAwait(false);
	}

	private NpgsqlConnection CreateConnection() => new(_cnn.ConnectionString);

	public async Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync()
	{
		const string query = """
			select version
			     , name
			     , item_type as ItemType
			     , relative_path as RelativePath
			     , status
			     , content_hash as ContentHash
			     , created_utc as CreatedUtc
			     , applied_utc as AppliedUtc
			     , execution_time_ms as ExecutionTimeMs
			from dblive.migration
		""";

		using NpgsqlConnection cnn = CreateConnection();
		return (await cnn.QueryAsync<MigrationItemRow>(query).ConfigureAwait(false)).Select(r => r.ToDto()).ToList();
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

	public async Task<IReadOnlyCollection<long>> GetAppliedMigrationVersionsAsync()
	{
		const string query = """
			select version
			from dblive.migration
			where item_type = 'migration'
			  and status = 'applied'
		""";

		using NpgsqlConnection cnn = CreateConnection();
		return (await cnn.QueryAsync<long>(query).ConfigureAwait(false)).ToList();
	}

	public async Task<long> GetSelfMigrationVersionAsync()
	{
		const string query = "select version from dblive.version;";
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.ExecuteScalarAsync<long?>(query).ConfigureAwait(false) ?? 0;
	}

	public async Task SetSelfMigrationVersionAsync(long version, DateTime migrationDateTime)
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

	// the column comes with self-migration 9: a database deployed before it has none, and a query that names
	// a missing column fails. The row as json has the key only when the column exists, so the value is null without it.
	public async Task<Version?> GetMinDbLiveLibVersionAsync()
	{
		const string query = "select to_jsonb(v) ->> 'min_dblive_lib_version' from dblive.version as v;";
		using NpgsqlConnection cnn = CreateConnection();
		string? version = await cnn.ExecuteScalarAsync<string?>(query).ConfigureAwait(false);
		return version is null ? null : Version.Parse(version);
	}

	public async Task SetMinDbLiveLibVersionAsync(Version version)
	{
		const string query = """
			update dblive.version
			set min_dblive_lib_version = @min_dblive_lib_version;
		""";

		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(query, new { min_dblive_lib_version = version.ToString() }).ConfigureAwait(false);
	}

	// the column comes with self-migration 3, and the ProjectId is checked before the self-deployment: a database deployed
	// before it has none. The row as json has the key only when the column exists, so the value is null without it.
	public async Task<string?> GetProjectIdAsync()
	{
		const string query = "select to_jsonb(v) ->> 'project_id' from dblive.version as v;";
		using NpgsqlConnection cnn = CreateConnection();
		return await cnn.ExecuteScalarAsync<string?>(query).ConfigureAwait(false);
	}

	public async Task SetProjectIdAsync(string projectId)
	{
		const string query = """
			update dblive.version
			set project_id = @project_id;
		""";

		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(query, new { project_id = ProjectIdTools.Normalize(projectId) }).ConfigureAwait(false);
	}

	public async Task ExecuteNonQueryAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	)
	{
		try
		{
			using NpgsqlConnection cnn = CreateConnection();
			await cnn.OpenAsync().ConfigureAwait(false);

			if (System.Transactions.Transaction.Current is null)
			{
				await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);
			}

			ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);

			using NpgsqlCommand cmd = new(sqlStatement, cnn);
			cmd.CommandTimeout = scriptTimeout.GetCommandTimeoutSeconds();

			try
			{
				_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			catch (NpgsqlException e) when (IsCommandTimeout(e) && !IsTransactionAborted())
			{
				throw scriptTimeout.Expired(e);
			}
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			throw new DbLiveSqlException(GetMessage(e), GetSqlError(e), e);
		}
	}

	// Npgsql reports a command timeout as NpgsqlException "Exception while reading from stream" with TimeoutException "Timeout during
	// reading attempt" inside, also when it has cancelled the statement on the server. A connection timeout has a TimeoutException
	// inside too, so only the command of a script is checked for it.
	private static bool IsCommandTimeout(NpgsqlException e) => e.InnerException is TimeoutException;

	// when System.Transactions aborts the transaction at its timeout while a command runs, Npgsql cancels the command to roll back, and
	// the cancelled command has the shape of a command timeout. It is not the timeout of the script then: it stays the error of the
	// provider, and the scope of DbLive makes it the timeout of the transaction (TransactionTimeout).
	private static bool IsTransactionAborted() =>
		System.Transactions.Transaction.Current?.TransactionInformation.Status == System.Transactions.TransactionStatus.Aborted;

	// 'set transaction' outside a transaction block only emits a warning and has no effect.
	// The session default applies to the implicit transaction of each next command on this connection,
	// also to a command that cannot run in a transaction block ('create index concurrently', 'vacuum').
	// Npgsql resets the session ('discard all') when the connection goes back to the pool.
	private static async Task SetTransactionIsolationLevelAsync(NpgsqlConnection cnn, TranIsolationLevel isolationLevel)
	{
		string isolationLevelSql = TranIsolationLevelMapper.ToSql(isolationLevel, DbProvider.PostgreSql);

		using NpgsqlCommand cmd = new($"set session characteristics as transaction isolation level {isolationLevelSql};", cnn);
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	public async Task<List<SqlResult>> ExecuteQueryMultipleAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	)
	{
		try
		{
			using NpgsqlConnection cnn = CreateConnection();
			await cnn.OpenAsync().ConfigureAwait(false);

			if (System.Transactions.Transaction.Current is null)
			{
				await SetTransactionIsolationLevelAsync(cnn, isolationLevel).ConfigureAwait(false);
			}

			ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);

			using NpgsqlCommand cmd = new(sqlStatement, cnn);
			cmd.CommandTimeout = scriptTimeout.GetCommandTimeoutSeconds();

			try
			{
				using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo).ConfigureAwait(false);

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
			catch (NpgsqlException e) when (IsCommandTimeout(e) && !IsTransactionAborted())
			{
				throw scriptTimeout.Expired(e);
			}
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			throw new DbLiveSqlException(GetMessage(e), GetSqlError(e), e);
		}
	}

	private static async Task<SqlResult?> ReadResultAsync(NpgsqlDataReader reader)
	{
		if (!reader.HasRows && reader.FieldCount == 0) return null;

		ReadOnlyCollection<DbColumn> columnSchema = await reader.GetColumnSchemaAsync().ConfigureAwait(false);

		List<SqlColumn> sqlColumns = [];
		foreach (DbColumn col in columnSchema)
		{
			sqlColumns.Add(new SqlColumn(
				columnName: col.ColumnName,
				dataType: GetPostgresTypeName(col)
			));
		}

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

		return new SqlResult(sqlColumns, rows);
	}

	private static string GetPostgresTypeName(DbColumn colraw)
	{
		var col = colraw as NpgsqlDbColumn;

		if (col is null) throw new InvalidOperationException("Expected NpgsqlDbColumn.");

		string typeName = col.DataTypeName?.ToLowerInvariant() ?? "unknown";

		return typeName switch
		{
			"varchar" or "character varying" => col.ColumnSize > 0 && col.ColumnSize < int.MaxValue
				? $"varchar({col.ColumnSize})"
				: "text",

			"bpchar" or "character" => $"char({col.ColumnSize ?? 1})",

			"numeric" or "decimal" => col.NumericPrecision.HasValue
				? (col.NumericScale.HasValue ? $"numeric({col.NumericPrecision},{col.NumericScale})" : $"numeric({col.NumericPrecision})")
				: "numeric",

			"timestamp" or "timestamp without time zone" => col.NumericScale.HasValue
				? $"timestamp({col.NumericScale})"
				: "timestamp",

			"timestamptz" or "timestamp with time zone" => col.NumericScale.HasValue
				? $"timestamptz({col.NumericScale})"
				: "timestamptz",

			"time" or "time without time zone" => col.NumericScale.HasValue
				? $"time({col.NumericScale})"
				: "time",

			"bit" => col.ColumnSize > 1 ? $"bit({col.ColumnSize})" : "bit",
			"varbit" or "bit varying" => $"varbit({col.ColumnSize})",

			"int4" => "integer",
			"int8" => "bigint",
			"int2" => "smallint",
			"float4" => "real",
			"float8" => "double precision",
			"bool" => "boolean",

			_ => typeName
		};
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
		using NpgsqlCommand cmd = cnn.CreateCommand();
		var quotedDbName = new NpgsqlCommandBuilder().QuoteIdentifier(dbName!);
		cmd.CommandText = $"CREATE DATABASE {quotedDbName};";
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	public async Task CheckDatabaseExistsAsync()
	{
		try
		{
			using NpgsqlConnection cnn = CreateConnection();
			await cnn.OpenAsync().ConfigureAwait(false);
		}
		// a login without access to the database gets 42501 (insufficient_privilege) instead.
		catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InvalidCatalogName)
		{
			NpgsqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
			throw new DatabaseNotFoundException(builder.Database ?? "", builder.Host ?? "", e);
		}
	}

	/// <summary>
	/// The key of the deployment lock: the ASCII codes of "dblive". An advisory lock is local to the database; <c>pg_locks</c> shows
	/// this key as <c>classid = 25698, objid = 1818850917, objsubid = 1</c>.
	/// </summary>
	internal const long DeploymentLockKey = 0x64626C697665;

	public async Task<IAsyncDisposable> AcquireDeploymentLockAsync()
	{
		NpgsqlConnectionStringBuilder builder = new(_cnn.ConnectionString) { Pooling = false, Enlist = false };
		NpgsqlConnection cnn = new(builder.ConnectionString);

		try
		{
			await cnn.OpenAsync().ConfigureAwait(false);

			// a session lock, taken outside a transaction: the session stays idle, not idle in transaction, for the whole deployment.
			bool acquired = await cnn.ExecuteScalarAsync<bool>(
				"select pg_try_advisory_lock(@key);",
				new { key = DeploymentLockKey }
			).ConfigureAwait(false);

			if (!acquired)
			{
				throw new DeploymentInProgressException(builder.Database ?? "", builder.Host ?? "");
			}

			return new DeploymentLock(
				cnn,
				c => c.ExecuteAsync("select pg_advisory_unlock(@key);", new { key = DeploymentLockKey })
			);
		}
		catch
		{
			await cnn.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	public async Task DropDbAsync(bool skipIfNotExists = true)
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
		""").ConfigureAwait(false);

		_ = await cnn.ExecuteAsync(
			"select pg_terminate_backend(pid) from pg_stat_activity where datname = @name;",
			new { name = dbName }
		).ConfigureAwait(false);

		_ = await cnn.ExecuteAsync($"""
			drop database "{dbName}";
		""").ConfigureAwait(false);
	}


	// relative_path is stored in lower case (ToStoredPath), so a file is identified ignoring case, the same way it is found in the project.
	public async Task SaveCodeItemAsync(CodeItemDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
				insert into dblive.code (
					relative_path, item_type, status, content_hash, applied_utc,
					execution_time_ms, created_utc, error
				)
				values (
					@relative_path, @item_type, @status, @content_hash, @applied_utc,
					@execution_time_ms, @created_utc, @error
				)
				on conflict (relative_path) do update set
					item_type = excluded.item_type,
					status = excluded.status,
					content_hash = excluded.content_hash,
					applied_utc = excluded.applied_utc,
					execution_time_ms = excluded.execution_time_ms,
					error = excluded.error;
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
				error = item.ErrorMessage
			}
		).ConfigureAwait(false);
	}

	// the paths are sent as one text[] parameter.
	public async Task MarkCodeAsVerifiedAsync(IReadOnlyCollection<string> relativePaths, DateTime verifiedUtc)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(
			"""
			update dblive.code
			set verified_utc = @verified_utc
			where relative_path in ( select p from unnest(@paths) as p )
			""",
			new { paths = relativePaths.Select(p => p.ToStoredPath()).ToArray(), verified_utc = verifiedUtc }
		).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<CodeItemDto>> GetCodeItemsAsync()
	{
		using NpgsqlConnection cnn = CreateConnection();
		return (await cnn.QueryAsync<CodeItemDto>(
			"""
			select relative_path as RelativePath
				 , item_type as ItemType
				 , content_hash as ContentHash
				 , applied_utc as AppliedUtc
				 , created_utc as CreatedUtc
				 , execution_time_ms as ExecutionTimeMs
				 , verified_utc as VerifiedUtc
				 , error as ErrorMessage
				 , status
			from dblive.code
			"""
		).ConfigureAwait(false)).ToList();
	}

	// the project paths are sent as one text[] parameter.
	public async Task<IReadOnlyCollection<string>> MarkMissingCodeAsRemovedAsync(IReadOnlyCollection<string> projectRelativePaths, DateTime verifiedUtc)
	{
		using NpgsqlConnection cnn = CreateConnection();
		return (await cnn.QueryAsync<string>(
			"""
			update dblive.code
			set status = 'removed'
			  , verified_utc = @verified_utc
			where status <> 'removed'
			  and relative_path not in ( select p from unnest(@paths) as p )
			returning relative_path
			""",
			new { paths = projectRelativePaths.Select(p => p.ToStoredPath()).ToArray(), verified_utc = verifiedUtc }
		).ConfigureAwait(false)).ToList();
	}

	public async Task SaveMigrationItemAsync(MigrationItemSaveDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
			insert into dblive.migration (
				version, name, item_type, relative_path,
				status, content_hash, created_utc
			)
			values (
				@version, @name, @item_type, @relative_path,
				@status, @content_hash, @created_utc
			)
			on conflict (version, item_type) do update set
				status = excluded.status,
				content_hash = excluded.content_hash,
				relative_path = excluded.relative_path;
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
		}).ConfigureAwait(false);
	}

	public async Task RepairMigrationChecksumAsync(MigrationChecksumRepairDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		int rowsAffected = await cnn.ExecuteAsync("""
			update dblive.migration
			set content_hash = @content_hash,
				relative_path = @relative_path
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
		}).ConfigureAwait(false);

		if (rowsAffected == 0)
		{
			throw new DbLiveMigrationItemMissedSqlException(
				$"Applied migration item not found for checksum repair. Version: {item.Version}, Type: {item.ItemType}");
		}
	}

	public async Task UpdateMigrationStateAsync(MigrationItemStateDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();

		long? ver = await cnn.QueryFirstOrDefaultAsync<long?>("""
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
			item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
			status = item.Status.ToString().ToLowerInvariant(),
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
			relative_path = item.RelativePath.ToStoredPath(),
			content_hash = item.ContentHash,
			run_utc = item.StartedUtc,
			execution_time_ms = item.ExecutionTimeMs,
			pass = item.IsSuccess,
			error = item.ErrorMessage
		}).ConfigureAwait(false);
	}

	public async Task<SchemaSnapshot> GetSchemaSnapshotAsync()
	{
		using NpgsqlConnection cnn = CreateConnection();
		IEnumerable<SchemaObjectRow> rows = await cnn.QueryAsync<SchemaObjectRow>(PostgreSqlSchemaSnapshot.Query).ConfigureAwait(false);
		return new SchemaSnapshot(rows.Select(r => PostgreSqlSchemaSnapshot.Normalize(r.ToSchemaObject())));
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
			relative_path = relativePath.ToStoredPath(),
			started_utc = startedUtc,
			completed_utc = completedUtc,
			execution_time_ms = executionTimeMs,
			content_hash = contentHash
		}).ConfigureAwait(false);
	}

	/// <summary>
	/// The message of an error of the server, or the distinct messages of the exception chain, one per line.
	/// Npgsql reports a lost connection as <see cref="NpgsqlException"/> "Exception while reading from stream", and the cause is in its
	/// inner exception. A command timeout has the same shape, but it becomes <see cref="ExecutionTimeoutException"/>; the cancel of a
	/// command when its transaction timed out stays this error.
	/// </summary>
	internal static string GetMessage(Exception ex)
	{
		if (ex is PostgresException)
		{
			return ex.Message;
		}

		List<string> messages = [];
		for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
		{
			if (!messages.Contains(cur.Message))
			{
				messages.Add(cur.Message);
			}
		}

		return string.Join(Environment.NewLine, messages);
	}

	private static string GetSqlError(Exception ex)
	{
		if (ex is PostgresException pgex)
		{
			return $"""
				PostgreSQL error:
				Severity: {pgex.Severity}
				SqlState: {pgex.SqlState}
				Message: {pgex.Message}
				Position: {pgex.Position}
				InternalQuery: {pgex.InternalQuery}
				Where: {pgex.Where}
				Routine: {pgex.Routine}
				""";
		}

		return "";
	}
}
