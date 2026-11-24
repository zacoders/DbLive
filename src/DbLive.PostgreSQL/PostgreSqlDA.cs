using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using DbLive.Schema;
using Npgsql.Schema;
using NpgsqlTypes;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Net;

namespace DbLive.PostgreSQL;

internal class PostgreSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	public DbProvider Provider => DbProvider.PostgreSql;

	// an error aborts the transaction: every later statement fails with 25P02 until the rollback to a savepoint or of the transaction.
	// A script that ended it with COMMIT or ROLLBACK leaves no error: the next commands run in transactions of their own, where
	// 'select 1' succeeds. Those have no ID of a transaction yet, and a transaction of DbLive has one: ExecuteGuardedAsync assigns it
	// before every script in it.
	public async Task<bool> IsTransactionUsableAsync()
	{
		using NpgsqlConnection cnn = CreateConnection();
		try
		{
			return await cnn.ExecuteScalarAsync<bool>("select pg_current_xact_id_if_assigned() is not null").ConfigureAwait(false);
		}
		catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InFailedSqlTransaction)
		{
			return false;
		}
	}

	// a file is one command, which runs as one implicit transaction whatever DbLive does.
	public bool RunsCodeItemWithoutTransaction(string sqlStatement) => false;

	// the path is text, and a b-tree index entry holds about 2700 bytes after compression: how long a path fits depends on its
	// characters and on how well it compresses, so no length is checked. A path that long is far beyond any real project.
	public int? MaxCodePathLength => null;

	public int? MaxFolderItemPathLength => null;

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
		// outside the deployment transaction the file runs between begin and commit of its own, as on SQL Server: one command would be one
		// implicit transaction anyway, but a COMMIT or ROLLBACK in the file would end it unnoticed.
		if (System.Transactions.Transaction.Current is null)
		{
			await ExecuteScriptAsync(sqlStatement, isolationLevel, timeout, ownTransaction: true).ConfigureAwait(false);
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

	// Pooling=false works: Npgsql keeps the connection of a transaction for the next connection opened in it (verified).
	public void CheckConnectionString()
	{
		if (!new NpgsqlConnectionStringBuilder(_cnn.ConnectionString).Enlist) throw ConnectionStringNotSupported.Enlist();
	}

	private NpgsqlConnection CreateConnection() => new(_cnn.ConnectionString);

	// Npgsql loads the types of the database when the first connection of the connection string opens, in a deployment before the
	// migrations, and keeps them for the process, also with Pooling=false. A column or a value of an enum, a domain or an array of them
	// that the deployment created then failed to read: KeyNotFoundException from the column schema of a SQL test, InvalidCastException
	// from the value. The reload on one connection reaches every later connection of the connection string, also a pooled one opened
	// before it (all verified with Npgsql 10.0.1).
	public async Task OnDeployedAsync()
	{
		using NpgsqlConnection cnn = CreateConnection();
		await cnn.OpenAsync().ConfigureAwait(false);
		await cnn.ReloadTypesAsync().ConfigureAwait(false);
	}

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

	public Task ExecuteNonQueryAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	) => ExecuteScriptAsync(sqlStatement, isolationLevel, timeout, ownTransaction: false);

	private async Task ExecuteScriptAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout,
		bool ownTransaction
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

			if (ownTransaction)
			{
				await ExecuteInOwnTransactionAsync(cnn, sqlStatement, timeout).ConfigureAwait(false);
			}
			else if (System.Transactions.Transaction.Current is not null)
			{
				_ = await ExecuteGuardedAsync(cnn, sqlStatement, script => ExecuteCommandAsync(cnn, script, timeout)).ConfigureAwait(false);
			}
			else
			{
				_ = await ExecuteCommandAsync(cnn, sqlStatement, timeout).ConfigureAwait(false);
			}
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			throw new DbLiveSqlException(GetMessage(e), GetSqlError(e), e);
		}
	}

	private static async Task<int> ExecuteCommandAsync(NpgsqlConnection cnn, string sqlStatement, TimeSpan timeout)
	{
		ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);
		using CancellationTokenSource cancellation = scriptTimeout.CreateCancellation();
		using NpgsqlCommand cmd = CreateScriptCommand(cnn, sqlStatement);

		try
		{
			return await cmd.ExecuteNonQueryAsync(cancellation.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException e) when (cancellation.IsCancellationRequested && !IsTransactionAborted())
		{
			throw scriptTimeout.Expired(e);
		}
	}

	// the timeout of a script is the cancellation of ScriptTimeout, not CommandTimeout: Npgsql starts its command timeout again on every
	// notice of the server, so a script with 'raise notice' in a loop, or a 'drop ... if exists' before a long statement, ran past it
	// (verified with Npgsql 10.0.1). 0 is no command timeout, also not the default of the connection string, 30 seconds.
	// When the time runs out, Npgsql cancels the statement on the server and throws OperationCanceledException.
	private static NpgsqlCommand CreateScriptCommand(NpgsqlConnection cnn, string sqlStatement) => new(sqlStatement, cnn) { CommandTimeout = 0 };

	// raw begin and commit on the connection, not NpgsqlTransaction: it refuses every command after a COMMIT or ROLLBACK of the script,
	// and the check of ExecuteGuardedAsync is what finds those.
	private static async Task ExecuteInOwnTransactionAsync(NpgsqlConnection cnn, string sqlStatement, TimeSpan timeout)
	{
		_ = await cnn.ExecuteAsync("begin;").ConfigureAwait(false);
		try
		{
			_ = await ExecuteGuardedAsync(cnn, sqlStatement, script => ExecuteCommandAsync(cnn, script, timeout)).ConfigureAwait(false);
			_ = await cnn.ExecuteAsync("commit;").ConfigureAwait(false);
		}
		catch
		{
			await TryRollbackAsync(cnn).ConfigureAwait(false);
			throw;
		}
	}

	// the error may have broken the connection, and it is the error to throw. Without a transaction in progress 'rollback' is a warning.
	private static async Task TryRollbackAsync(NpgsqlConnection cnn)
	{
		try
		{
			_ = await cnn.ExecuteAsync("rollback;").ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
	}

	internal const string TransactionEndedExample =
		"for example BEGIN ... COMMIT around the script: BEGIN inside a transaction is only a warning on PostgreSQL";

	// not a code of PostgreSQL: its classes of SQLSTATE do not use DL.
	internal const string TransactionEndedSqlState = "DL001";

	/// <summary>
	/// Runs a script in the transaction of DbLive, the ambient one or the own transaction of a code file, and fails when the script ended
	/// it: a top-level <c>COMMIT</c> or <c>ROLLBACK</c>, which PostgreSQL runs without an error, also after a <c>BEGIN</c> of the script
	/// (only a warning in a transaction). The ID of the transaction, read before the script, is compared at the end of the same command:
	/// Npgsql sends its statements with one <c>Sync</c>, so the statements after the end of the transaction run in one implicit transaction
	/// up to it, and the error of the check rolls them back. What the script committed with its <c>COMMIT</c> stays, nothing else.
	/// Reading the ID assigns one to the transaction, which <see cref="IsTransactionUsableAsync"/> relies on.
	/// </summary>
	/// <exception cref="DbLiveTransactionEndedException">The script ended the transaction.</exception>
	private static async Task<T> ExecuteGuardedAsync<T>(NpgsqlConnection cnn, string sqlStatement, Func<string, Task<T>> execute)
	{
		string transactionId = (await cnn.ExecuteScalarAsync<string>("select pg_current_xact_id()::text").ConfigureAwait(false))!;

		try
		{
			return await execute(AddTransactionCheck(sqlStatement, transactionId)).ConfigureAwait(false);
		}
		catch (PostgresException e) when (e.SqlState == TransactionEndedSqlState)
		{
			// a script that began a transaction of its own after the end (rollback; begin; ...) leaves it aborted by the check.
			await TryRollbackAsync(cnn).ConfigureAwait(false);
			throw new DbLiveTransactionEndedException(TransactionEndedExample);
		}
	}

	// after a line end, so that a script that ends with a '--' comment does not swallow it, and after a semicolon, so that a script that
	// ends without one does not run into it. A statement of its own: the positions of the errors of the script do not change.
	internal static string AddTransactionCheck(string sqlStatement, string transactionId) => $"""
		{sqlStatement}
		;
		do $dblive$ begin
			if pg_current_xact_id() <> '{transactionId}'::xid8 then
				raise exception using errcode = '{TransactionEndedSqlState}', message = 'The script ended the transaction that DbLive runs it in.';
			end if;
		end $dblive$;
		""";

	// when System.Transactions aborts the transaction at its timeout while a command runs, Npgsql cancels the command to roll back. When
	// the time of the script runs out at the same moment (a migration has the same timeout as its transaction), it is not the timeout of
	// the script: it stays the error of the provider, and the scope of DbLive makes it the timeout of the transaction (TransactionTimeout).
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

			// a SQL test runs in a transaction of DbLive, which it must not end. The check adds a statement without a result set.
			return System.Transactions.Transaction.Current is null
				? await QueryMultipleAsync(cnn, sqlStatement, timeout).ConfigureAwait(false)
				: await ExecuteGuardedAsync(cnn, sqlStatement, script => QueryMultipleAsync(cnn, script, timeout)).ConfigureAwait(false);
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			throw new DbLiveSqlException(GetMessage(e), GetSqlError(e), e);
		}
	}

	// the reader is closed when it returns: the connection takes no other command while a reader is open.
	// Every call of the reader takes the token: the server runs the statements after the first result set while the reader moves on.
	private static async Task<List<SqlResult>> QueryMultipleAsync(NpgsqlConnection cnn, string sqlStatement, TimeSpan timeout)
	{
		ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);
		using CancellationTokenSource cancellation = scriptTimeout.CreateCancellation();
		using NpgsqlCommand cmd = CreateScriptCommand(cnn, sqlStatement);

		try
		{
			using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo, cancellation.Token).ConfigureAwait(false);

			List<SqlResult> results = [];

			do
			{
				SqlResult? r = await ReadResultAsync(reader, cancellation.Token).ConfigureAwait(false);
				if (r is not null)
				{
					results.Add(r);
				}
			}
			while (await reader.NextResultAsync(cancellation.Token).ConfigureAwait(false));

			return results;
		}
		catch (OperationCanceledException e) when (cancellation.IsCancellationRequested && !IsTransactionAborted())
		{
			throw scriptTimeout.Expired(e);
		}
	}

	private static async Task<SqlResult?> ReadResultAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
	{
		if (!reader.HasRows && reader.FieldCount == 0) return null;

		ReadOnlyCollection<DbColumn> columnSchema = await reader.GetColumnSchemaAsync(cancellationToken).ConfigureAwait(false);

		List<SqlColumn> sqlColumns = [];
		foreach (DbColumn col in columnSchema)
		{
			sqlColumns.Add(new SqlColumn(
				columnName: col.ColumnName,
				dataType: GetPostgresTypeName(col)
			));
		}

		List<SqlRow> rows = [];
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			SqlRow r = [];
			for (int i = 0; i < reader.FieldCount; i++)
			{
				r.Add(ReadValue(reader, i));
			}
			rows.Add(r);
		}

		return new SqlResult(sqlColumns, rows);
	}

	// Npgsql reads inet as IPAddress, which has no netmask, so rows took '192.168.0.1/24'::inet for '192.168.0.1/16'::inet. NpgsqlInet
	// keeps it, and its text is the one PostgreSQL shows: '192.168.0.1/24', and '192.168.0.1' for a single host (/32 or /128).
	// A multidimensional inet[] stays as Npgsql reads it.
	private static object ReadValue(NpgsqlDataReader reader, int ordinal) =>
		reader.GetValue(ordinal) switch
		{
			IPAddress => reader.GetFieldValue<NpgsqlInet>(ordinal).ToString(),
			IPAddress[] => reader.GetFieldValue<NpgsqlInet?[]>(ordinal).Select(inet => inet?.ToString()).ToArray(),
			object value => value
		};

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

			// an unbounded bpchar keeps the whole string, char is char(1)
			"bpchar" or "character" => col.ColumnSize.HasValue ? $"char({col.ColumnSize})" : "bpchar",

			"numeric" or "decimal" => col.NumericPrecision.HasValue
				? (col.NumericScale.HasValue ? $"numeric({col.NumericPrecision},{col.NumericScale})" : $"numeric({col.NumericPrecision})")
				: "numeric",

			"timestamp" or "timestamp without time zone" => WithSecondsPrecision("timestamp", col),
			"timestamptz" or "timestamp with time zone" => WithSecondsPrecision("timestamptz", col),
			"time" or "time without time zone" => WithSecondsPrecision("time", col),
			"timetz" or "time with time zone" => WithSecondsPrecision("timetz", col),
			"interval" => WithSecondsPrecision("interval", col),

			"bit" => col.ColumnSize > 1 ? $"bit({col.ColumnSize})" : "bit",
			"varbit" or "bit varying" => col.ColumnSize.HasValue ? $"varbit({col.ColumnSize})" : "varbit",

			"int4" => "integer",
			"int8" => "bigint",
			"int2" => "smallint",
			"float4" => "real",
			"float8" => "double precision",
			"bool" => "boolean",

			_ => typeName
		};
	}

	// Npgsql gives the precision of the fractional seconds in NumericPrecision: null without one, 65535 for an interval with fields
	// (interval year) and without a precision. The fields themselves it does not give. 6 is the largest precision, and a type without
	// one keeps microseconds too, so timestamp(6) is named timestamp, as datetime2(7) and datetime2 are one type on SQL Server.
	private static string WithSecondsPrecision(string typeName, NpgsqlDbColumn col) =>
		col.NumericPrecision is int precision and < 6 ? $"{typeName}({precision})" : typeName;

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

		using NpgsqlCommand cmd = cnn.CreateCommand();
		cmd.CommandText = $"CREATE DATABASE {QuoteDbName(dbName!)};";
		cmd.CommandTimeout = CreateDropTimeoutSeconds;
		_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	// the name in double quotes, with '"' doubled: a database name may have any character, also a '"'. Quoted, the case of the name
	// stays as in the connection string.
	private static string QuoteDbName(string name)
	{
		using NpgsqlCommandBuilder builder = new();
		return builder.QuoteIdentifier(name);
	}

	// as on SQL Server, instead of the 30 seconds of Npgsql: both wait for the files. Before PostgreSQL 15 CREATE DATABASE copies the
	// template with checkpoints (FILE_COPY), which a busy server makes slow.
	private const int CreateDropTimeoutSeconds = 600;

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

	/// <summary>
	/// Seconds without traffic after which the connection of the deployment lock sends a keep-alive: Npgsql <c>KeepAlive</c> on the client,
	/// <c>tcp_keepalives_idle</c> on the server. Under the idle timeout of a NAT gateway or a load balancer (AWS NAT Gateway 350 seconds,
	/// Azure 4 minutes), and as on SQL Server.
	/// </summary>
	internal const int DeploymentLockKeepAliveSeconds = 30;

	public async Task<IAsyncDisposable> AcquireDeploymentLockAsync()
	{
		NpgsqlConnectionStringBuilder builder = new(_cnn.ConnectionString) { Pooling = false, Enlist = false };

		// the connection sends nothing between the lock and its release, for the whole deployment. A device on the way that drops an idle
		// connection would leave the lock held on the server after the deployment, and idle_session_timeout would end the session in the
		// middle of it. The keep-alive of Npgsql is a message of the protocol: activity of the session also for idle_session_timeout, unlike
		// a TCP keep-alive. A keep-alive of the connection string stays.
		if (builder.KeepAlive == 0)
		{
			builder.KeepAlive = DeploymentLockKeepAliveSeconds;
		}

		NpgsqlConnection cnn = new(builder.ConnectionString);

		try
		{
			await cnn.OpenAsync().ConfigureAwait(false);

			// the server ends the session of a deployer that lost its network within about a minute, not after tcp_keepalives_idle of the
			// operating system (2 hours on Linux), and its keep-alives also keep a device on the way from dropping the connection.
			// Settings of the session, which any user may change; ignored on a Unix-domain socket.
			_ = await cnn.ExecuteAsync($"""
				set tcp_keepalives_idle = {DeploymentLockKeepAliveSeconds};
				set tcp_keepalives_interval = 10;
				set tcp_keepalives_count = 3;
				""").ConfigureAwait(false);

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

		string quotedName = QuoteDbName(dbName!);

		_ = await cnn.ExecuteAsync($"""
			revoke connect on database {quotedName} from public;
		""").ConfigureAwait(false);

		_ = await cnn.ExecuteAsync(
			"select pg_terminate_backend(pid) from pg_stat_activity where datname = @name;",
			new { name = dbName }
		).ConfigureAwait(false);

		_ = await cnn.ExecuteAsync($"""
			drop database {quotedName};
		""", commandTimeout: CreateDropTimeoutSeconds).ConfigureAwait(false);
	}


	// relative_path is stored in lower case (ToStoredPath), so a file is identified ignoring case, the same way it is found in the project.
	public async Task SaveCodeItemAsync(CodeItemDto item)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync("""
				insert into dblive.code (
					relative_path, item_type, status, content_hash, applied_utc,
					execution_time_ms, created_utc
				)
				values (
					@relative_path, @item_type, @status, @content_hash, @applied_utc,
					@execution_time_ms, @created_utc
				)
				on conflict (relative_path) do update set
					item_type = excluded.item_type,
					status = excluded.status,
					content_hash = excluded.content_hash,
					applied_utc = excluded.applied_utc,
					execution_time_ms = excluded.execution_time_ms;
			""",
			new
			{
				relative_path = item.RelativePath.ToStoredPath(),
				item_type = item.ItemType.ToString().ToLowerInvariant(),
				status = item.Status.ToString().ToLowerInvariant(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc
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

	// the paths are sent as one text[] parameter.
	public async Task MarkCodeAsApplyingAsync(IReadOnlyCollection<string> relativePaths)
	{
		using NpgsqlConnection cnn = CreateConnection();
		_ = await cnn.ExecuteAsync(
			"""
			update dblive.code
			set status = 'applying'
			where status = 'applied'
			  and relative_path in ( select p from unnest(@paths) as p )
			""",
			new { paths = relativePaths.Select(p => p.ToStoredPath()).ToArray() }
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
				execution_time_ms = @execution_time_ms
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
			execution_time_ms = item.ExecutionTimeMs
		}).ConfigureAwait(false);

		if (ver is null)
		{
			throw new DbLiveMigrationItemMissedSqlException(
				$"Migration item not found. Version: {item.Version}, Type: {item.ItemType}"
			);
		}
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
	/// inner exception. The cancel of a command when its transaction timed out stays this error; the cancel at the timeout of the script
	/// becomes <see cref="ExecutionTimeoutException"/>.
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
