using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using DbLive.Schema;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DbLive.MSSQL;

internal partial class MsSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	public DbProvider Provider => DbProvider.MsSql;

	public async Task<bool> IsTransactionUsableAsync()
	{
		// a transaction rolled back by the server does not fail the next commands of the scope: they run in autocommit mode.
		// Transaction.Current stays Active then, only the server knows.
		using var cnn = GetConnection();
		return await cnn.ExecuteScalarAsync<int>("select @@trancount").ConfigureAwait(false) > 0;
	}

	// in the deployment transaction no savepoint: an error that does not roll back the whole transaction leaves it usable, and after one
	// that does (XACT_ABORT ON, a failed conversion, a deadlock) there is no savepoint to return to.
	// Outside it the file runs in a transaction of its own: its GO batches would commit one by one, and a batch that fails would leave
	// the batches before it applied. A file with In-Memory OLTP DDL runs without one, as DbLive 1.5 ran every code file.
	public Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	) => ExecuteScriptAsync(
		sqlStatement,
		isolationLevel,
		timeout,
		ownTransaction: Transaction.Current is null && !RunsCodeItemWithoutTransaction(sqlStatement)
	);

	// SQL Server does not allow the DDL of natively compiled modules and of memory-optimized objects in a user transaction: Msg 12331
	// rolls back the whole transaction, so the file could never deploy. The words are matched in the whole text: one in a comment or a
	// string only takes the transaction away from the file. DDL in dynamic SQL, or a drop that names neither word, still fails.
	public bool RunsCodeItemWithoutTransaction(string sqlStatement) => InMemoryOltpDdlRegex().IsMatch(sqlStatement);

	// whole words: not is_memory_optimized and uses_native_compilation of the catalog views, not memory_optimized_data of a filegroup.
	[GeneratedRegex(@"\b(?:native_compilation|memory_optimized)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex InMemoryOltpDdlRegex();

	// the relative path is the clustered primary key of dblive.code, and of dblive.folder_items after folder_type, and a clustered
	// index key holds at most 900 bytes: two per character of nvarchar, one per character of folder_type (varchar, the name of
	// ProjectFolder, BeforeDeploy at the longest). A longer path fails with Msg 1946 after its file ran.
	private const int MaxClusteredKeyBytes = 900;

	public int? MaxCodePathLength => MaxClusteredKeyBytes / 2;

	public int? MaxFolderItemPathLength => (MaxClusteredKeyBytes - Enum.GetNames<ProjectFolder>().Max(name => name.Length)) / 2;

	private SqlConnection GetConnection() => new(_cnn.ConnectionString);

	/// <summary>
	/// The parameters of a query from the properties of <paramref name="values"/>, a <see cref="DateTime"/> as <c>datetime2</c>: Dapper
	/// sends it as <c>datetime</c>, rounded to 1/300 of a second, and the dates of the dblive tables are <c>datetime2(7)</c>. Every query
	/// with a <see cref="DateTime"/> parameter takes its parameters from here.
	/// Not <c>SqlMapper.AddTypeMap</c>: the settings of Dapper are global for the process, also for PostgreSQL and for the queries of the
	/// application.
	/// </summary>
	private static DynamicParameters Parameters(object values)
	{
		DynamicParameters parameters = new();
		foreach (PropertyInfo property in values.GetType().GetProperties())
		{
			Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
			parameters.Add(property.Name, property.GetValue(values), type == typeof(DateTime) ? DbType.DateTime2 : null);
		}
		return parameters;
	}

	public async Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync()
	{
		const string query = @"
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
		_ = await cnn.ExecuteAsync(query, Parameters(new { version, applied_utc = migrationDateTime })).ConfigureAwait(false);
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
			// before the connection: a syntax error of a GO line fails the file before any batch runs, as it did when SMO ran the file.
			IReadOnlyList<ScriptBatch> batches = SplitIntoBatches(sqlStatement);

			using SqlConnection sqlConnection = GetConnection();
			await sqlConnection.OpenAsync().ConfigureAwait(false);
			if (Transaction.Current is null)
			{
				await SetTransactionIsolationLevelAsync(sqlConnection, isolationLevel).ConfigureAwait(false);
			}
			if (ownTransaction)
			{
				await ExecuteInOwnTransactionAsync(sqlConnection, batches, timeout).ConfigureAwait(false);
			}
			else if (Transaction.Current is not null)
			{
				_ = await ExecuteGuardedAsync(sqlConnection, () => ExecuteBatchesAsync(sqlConnection, batches, timeout)).ConfigureAwait(false);
			}
			else
			{
				await ExecuteBatchesAsync(sqlConnection, batches, timeout).ConfigureAwait(false);
			}
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			// not an error of a batch: begin or commit of the transaction of the file, the isolation level, the connection.
			// It has no line of the file.
			SqlException? sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, GetSqlServerError(sqlException), e);
		}
	}

	/// <summary>
	/// A <c>GO</c> batch of a script: its text, and the line of the file where it starts, from 1.
	/// </summary>
	internal sealed record ScriptBatch(string Text, int StartLine);

	/// <summary>
	/// The <c>GO</c> batches of a script, split by SMO in its capture mode: it splits the script as its <c>ExecuteNonQuery</c> does,
	/// runs nothing and needs no connection. <c>GO</c> alone on its line, in any case, with an optional count and <c>-- comment</c>;
	/// not inside comments, strings and brackets. The batch before <c>GO &lt;count&gt;</c> is in the list count times; <c>GO</c> on the
	/// first line and two <c>GO</c> in a row give an empty batch. A comment without its end and a <c>GO</c> line with anything else
	/// throw <c>BatchParserException</c>. The text of a batch is the piece of the file up to the line end before its <c>GO</c> line.
	/// <para>
	/// SMO gives the texts, not the lines where they start: the next batch starts after the line ends of the text and the <c>GO</c>
	/// line. The count of <c>GO &lt;count&gt;</c> is not read, it is rare: each repetition counts as a batch of its own, and the
	/// batches after it get lines further down than they are.
	/// </para>
	/// </summary>
	internal static IReadOnlyList<ScriptBatch> SplitIntoBatches(string script)
	{
		ServerConnection splitter = new() { SqlExecutionModes = SqlExecutionModes.CaptureSql };
		_ = splitter.ExecuteNonQuery(script);

		List<ScriptBatch> batches = [];
		int startLine = 1;

		foreach (string text in splitter.CapturedSql.Text.Cast<string>())
		{
			batches.Add(new ScriptBatch(text, startLine));
			startLine += CountLineEnds(text) + 1;
		}

		return batches;
	}

	// LF, CRLF and CR alone: SMO ends a line at CR alone too, as editors do.
	private static int CountLineEnds(string text)
	{
		int count = 0;

		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n' || (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n')))
			{
				count++;
			}
		}

		return count;
	}

	// each batch is a command of its own, and gets the time left of the file as its command timeout: the timeout is the timeout of the
	// file, not of each batch.
	// An error of a batch names the line of the file where the batch starts: SQL Server counts the line of an error from the start of
	// its batch, or of the procedure or the dynamic SQL where it is, and does not say which batch failed.
	private static async Task ExecuteBatchesAsync(SqlConnection sqlConnection, IReadOnlyList<ScriptBatch> batches, TimeSpan timeout)
	{
		ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);

		foreach (ScriptBatch batch in batches)
		{
			// SqlCommand fails on an empty text, and SMO did not run one either; a batch of white space does nothing.
			if (string.IsNullOrWhiteSpace(batch.Text))
			{
				continue;
			}

			using SqlCommand cmd = sqlConnection.CreateCommand();
			cmd.CommandText = batch.Text;
			cmd.CommandTimeout = scriptTimeout.GetCommandTimeoutSeconds();

			try
			{
				_ = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
			catch (SqlException e) when (IsCommandTimeout(e))
			{
				throw scriptTimeout.Expired(e);
			}
			catch (SqlException e)
			{
				throw new DbLiveSqlException(e.Message, GetSqlServerError(e, batch.StartLine), e);
			}
		}
	}

	// "Execution Timeout Expired". A connection timeout has the same number, so only the commands of a script are checked for it.
	private const int TimeoutExpiredError = -2;

	private static bool IsCommandTimeout(SqlException e) => e.Errors.Cast<SqlError>().Any(err => err.Number == TimeoutExpiredError);

	// a transaction of T-SQL on the connection, not a TransactionScope: it has no timeout of its own, and the isolation level set on the
	// connection applies to it.
	private static async Task ExecuteInOwnTransactionAsync(SqlConnection sqlConnection, IReadOnlyList<ScriptBatch> batches, TimeSpan timeout)
	{
		_ = await sqlConnection.ExecuteAsync("begin transaction;").ConfigureAwait(false);
		try
		{
			_ = await ExecuteGuardedAsync(sqlConnection, () => ExecuteBatchesAsync(sqlConnection, batches, timeout)).ConfigureAwait(false);
			_ = await sqlConnection.ExecuteAsync("commit transaction;").ConfigureAwait(false);
		}
		catch
		{
			await TryRollbackAsync(sqlConnection).ConfigureAwait(false);
			throw;
		}
	}

	internal const string TransactionEndedExample = "for example 'if @@trancount > 0 rollback' in a CATCH block that does not rethrow the error";

	// the ID before the script, then IMPLICIT_TRANSACTIONS: with the setting on, the read itself would open a transaction when the one
	// of DbLive is gone already, and the script would run in it unnoticed.
	private const string StartTransactionGuardSql = "select current_transaction_id(); set implicit_transactions on;";

	// another ID: the script ended the transaction of DbLive, and IMPLICIT_TRANSACTIONS made its statements after the end open a new one
	// instead of committing at once. It is rolled back, so that nothing of it stays and the caller finds no transaction on the connection.
	// None of these statements opens a transaction of its own with the setting on: a select without a table does not.
	private const string EndTransactionGuardSql = """
		declare @ended bit = iif(current_transaction_id() = @before, 0, 1);
		if @ended = 1 and @@trancount > 0 rollback;
		set implicit_transactions off;
		select @ended;
		""";

	/// <summary>
	/// Runs a script in the transaction of DbLive, the ambient one or the own transaction of a code file, and fails when the script ended
	/// it: <c>COMMIT</c> or <c>ROLLBACK</c> in the script. SQL Server does not tell: the statements after the end run in autocommit mode, and
	/// a script that begins a transaction of its own after it leaves <c>@@trancount</c> as it was, so the ID of the transaction is compared.
	/// With <c>IMPLICIT_TRANSACTIONS ON</c> the statements after the end open a new transaction instead, which is rolled back: what the
	/// script committed with its <c>COMMIT</c> stays, nothing else. While the transaction of DbLive is open, the setting changes nothing.
	/// Only for a script in a transaction: without one, every statement would open a transaction that nothing commits.
	/// </summary>
	/// <exception cref="DbLiveTransactionEndedException">The script ended the transaction.</exception>
	private static async Task<T> ExecuteGuardedAsync<T>(SqlConnection sqlConnection, Func<Task<T>> script)
	{
		long before = await sqlConnection.ExecuteScalarAsync<long>(StartTransactionGuardSql).ConfigureAwait(false);

		T result;
		try
		{
			result = await script().ConfigureAwait(false);
		}
		catch
		{
			// the error of the script is the one to throw, also when the script ended the transaction before it: an error can end it too
			// (XACT_ABORT ON, a failed conversion, a deadlock), and the two cannot be told apart.
			await TryEndTransactionGuardAsync(sqlConnection, before).ConfigureAwait(false);
			throw;
		}

		if (await sqlConnection.ExecuteScalarAsync<bool>(EndTransactionGuardSql, new { before }).ConfigureAwait(false))
		{
			throw new DbLiveTransactionEndedException(TransactionEndedExample);
		}

		return result;
	}

	private static Task<bool> ExecuteGuardedAsync(SqlConnection sqlConnection, Func<Task> script) =>
		ExecuteGuardedAsync(sqlConnection, async () =>
		{
			await script().ConfigureAwait(false);
			return true;
		});

	// the connection may be broken by the error of the script, which is the error to throw.
	private static async Task TryEndTransactionGuardAsync(SqlConnection sqlConnection, long before)
	{
		try
		{
			_ = await sqlConnection.ExecuteScalarAsync<bool>(EndTransactionGuardSql, new { before }).ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
	}

	// the error may have rolled the transaction back already (XACT_ABORT ON, a failed conversion, a deadlock) or broken the connection,
	// and it is the error to throw. A connection that went back to the pool with an open transaction would keep its locks.
	private static async Task TryRollbackAsync(SqlConnection sqlConnection)
	{
		try
		{
			_ = await sqlConnection.ExecuteAsync("if @@trancount > 0 rollback transaction;").ConfigureAwait(false);
		}
		catch (Exception)
		{
		}
	}

	private static async Task SetTransactionIsolationLevelAsync(SqlConnection cnn, TranIsolationLevel isolationLevel)
	{
		string isolationLevelSql = TranIsolationLevelMapper.ToSql(isolationLevel, DbProvider.MsSql);
		_ = await cnn.ExecuteAsync($"SET TRANSACTION ISOLATION LEVEL {isolationLevelSql};").ConfigureAwait(false);
	}

	public async Task<List<SqlResult>> ExecuteQueryMultipleAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
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

			// a SQL test runs in a transaction of DbLive, which it must not end.
			return Transaction.Current is null
				? await QueryMultipleAsync(cnn, sqlStatement, timeout).ConfigureAwait(false)
				: await ExecuteGuardedAsync(cnn, () => QueryMultipleAsync(cnn, sqlStatement, timeout)).ConfigureAwait(false);
		}
		catch (Exception e) when (e is not DbLiveSqlException)
		{
			SqlException? sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, GetSqlServerError(sqlException), e);
		}
	}

	// the reader is closed when it returns: the connection takes no other command while a reader is open.
	private static async Task<List<SqlResult>> QueryMultipleAsync(SqlConnection cnn, string sqlStatement, TimeSpan timeout)
	{
		ScriptTimeout scriptTimeout = ScriptTimeout.Start(timeout);

		using SqlCommand cmd = cnn.CreateCommand();
		cmd.CommandTimeout = scriptTimeout.GetCommandTimeoutSeconds();
		cmd.CommandText = sqlStatement;

		try
		{
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
		catch (SqlException e) when (IsCommandTimeout(e))
		{
			throw scriptTimeout.Expired(e);
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
			columnName: GetValue<string>(row["ColumnName"]),
			dataType: GetSqlTypeName(
				providerType: GetValue<int>(row["ProviderType"]),
				dataTypeName: GetValue<string>(row["DataTypeName"]),
				numericPrecision: GetValueN<short?>(row["NumericPrecision"]),
				numericScale: GetValue<short?>(row["NumericScale"]),
				columnSize: GetValue<int>(row["ColumnSize"])
			)
		);
	}

	// The ColumnSize that SqlClient gives the max types: nvarchar(max), varchar(max), varbinary(max).
	private const int MaxColumnSize = int.MaxValue;

	// The type of a column for rows-with-schema, as T-SQL writes it, with its length, precision or scale: columns of different types
	// never get the same name. A type without a case here (json and vector of SQL Server 2025, a type of a later version) gets the name
	// SqlClient gives, without its length. A CLR type (geography, geometry, hierarchyid, a type of the user) gets its own name: SqlClient
	// gives it with the current database and the schema, mydb.sys.geography.
	private static string GetSqlTypeName(int providerType, string dataTypeName, short? numericPrecision, short? numericScale, int columnSize)
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
			SqlDbType.NVarChar => columnSize == MaxColumnSize ? "nvarchar(max)" : $"nvarchar({columnSize})",
			SqlDbType.Real => "real",
			SqlDbType.UniqueIdentifier => "uniqueidentifier",
			SqlDbType.SmallDateTime => "smalldatetime",
			SqlDbType.SmallInt => "smallint",
			SqlDbType.SmallMoney => "smallmoney",
			SqlDbType.Text => "text",
			SqlDbType.Time => $"time({numericScale ?? 7})",
			SqlDbType.Timestamp => "timestamp",
			SqlDbType.TinyInt => "tinyint",
			SqlDbType.Udt => dataTypeName[(dataTypeName.LastIndexOf('.') + 1)..],
			SqlDbType.VarBinary => columnSize == MaxColumnSize ? "varbinary(max)" : $"varbinary({columnSize})",
			SqlDbType.VarChar => columnSize == MaxColumnSize ? "varchar(max)" : $"varchar({columnSize})",
			SqlDbType.Variant => "sql_variant",
			SqlDbType.Xml => "xml",
			_ => dataTypeName
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

		_ = await cnn.ExecuteAsync($"create database {QuoteDbName(databaseToCreate)};", commandTimeout: CreateDropTimeoutSeconds).ConfigureAwait(false);
	}

	// the name in brackets, with ']' doubled: a database name may have any character, also a ']'.
	private static string QuoteDbName(string name)
	{
		using SqlCommandBuilder builder = new();
		return builder.QuoteIdentifier(name);
	}

	// the timeout of SMO, which ran the create and the drop before: both wait for the files, and the drop for the rollback of the
	// transactions of the sessions it ends.
	private const int CreateDropTimeoutSeconds = 600;

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

	/// <summary>
	/// The resource of the deployment lock. An application lock is local to the database.
	/// </summary>
	internal const string DeploymentLockResource = "dblive.deployment";

	public async Task<IAsyncDisposable> AcquireDeploymentLockAsync()
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString) { Pooling = false, Enlist = false };
		SqlConnection cnn = new(builder.ConnectionString);

		try
		{
			await cnn.OpenAsync().ConfigureAwait(false);

			// 0: granted. -1: another session holds the lock, and @LockTimeout = 0 does not wait for it. Below -1: an error of the call.
			int result = await cnn.ExecuteScalarAsync<int>("""
				declare @result int;
				exec @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
				select @result;
				""",
				new { resource = DeploymentLockResource }
			).ConfigureAwait(false);

			if (result == -1)
			{
				throw new DeploymentInProgressException(builder.InitialCatalog, builder.DataSource);
			}

			if (result < 0)
			{
				throw new InternalException($"sp_getapplock returned {result} for the deployment lock.");
			}

			return new DeploymentLock(
				cnn,
				c => c.ExecuteAsync(
					"exec sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';",
					new { resource = DeploymentLockResource }
				)
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

		await KillSessionsAsync(cnn, databaseToDrop).ConfigureAwait(false);

		// single_user ends the sessions that are left: of a login that may not kill them, or opened after the kill.
		// One batch: between two, another connection could take the single user of the database, and the drop would fail.
		string quotedName = QuoteDbName(databaseToDrop);
		_ = await cnn.ExecuteAsync($"""
			alter database {quotedName} set single_user with rollback immediate;
			drop database {quotedName};
			""",
			commandTimeout: CreateDropTimeoutSeconds
		).ConfigureAwait(false);
	}

	// a killed session that takes longer to end, a long rollback, is left to single_user.
	private static readonly TimeSpan KilledSessionsEndTimeout = TimeSpan.FromSeconds(2);

	/// <summary>
	/// Kills the sessions of the database and waits until they end, so that <c>single_user with rollback immediate</c> finds none: for
	/// any session it has to end, also an idle one of a connection pool or one of a connection closed just before, it waits about
	/// 3 seconds. Without the permissions (<c>VIEW SERVER STATE</c>, <c>ALTER ANY CONNECTION</c>) the login sees or kills nothing, and
	/// single_user ends the sessions, slower.
	/// </summary>
	private static async Task KillSessionsAsync(SqlConnection cnn, string database)
	{
		try
		{
			IEnumerable<short> sessions = await cnn.QueryAsync<short>("""
				select session_id
				from sys.dm_exec_sessions
				where database_id = db_id(@name) and is_user_process = 1 and session_id <> @@spid
				""",
				new { name = database }
			).ConfigureAwait(false);

			foreach (short session in sessions)
			{
				_ = await cnn.ExecuteAsync($"kill {session};").ConfigureAwait(false);
			}

			// a killed session ends asynchronously, as does the session of a connection closed just before the drop.
			DateTime deadline = DateTime.UtcNow + KilledSessionsEndTimeout;
			while (DateTime.UtcNow < deadline && await cnn.ExecuteScalarAsync<bool>("""
				select case when exists (
					select 1
					from sys.dm_tran_locks
					where resource_type = 'DATABASE' and resource_database_id = db_id(@name) and request_session_id <> @@spid
				) then 1 else 0 end
				""",
				new { name = database }
			).ConfigureAwait(false))
			{
				await Task.Delay(20).ConfigureAwait(false);
			}
		}
		catch (SqlException)
		{
			// no permission: to kill (6102), to read sys.dm_tran_locks (300); or a session ended before its kill (6106).
		}
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
			when not matched then
				insert (
					relative_path
					, item_type
					, status
					, content_hash
					, applied_utc
					, execution_time_ms
					, created_utc
				)
				values (
					@relative_path
					, @item_type
					, @status
					, @content_hash
					, @applied_utc
					, @execution_time_ms
					, @created_utc
				);
			""",
			Parameters(new
			{
				relative_path = item.RelativePath.ToStoredPath(),
				item_type = item.ItemType.ToString().ToLowerInvariant(),
				status = item.Status.ToString().ToLowerInvariant(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc
			})
		).ConfigureAwait(false);
	}

	/// <summary>
	/// The paths as one xml parameter <c>@paths</c>, whatever their number: a command takes at most 2100 parameters.
	/// <see cref="PathsTableSql"/> reads them into the temporary table <c>#paths</c>.
	/// </summary>
	private static XElement ToPathList(IEnumerable<string> relativePaths) =>
		new("paths", relativePaths.Select(p => new XElement("p", p.ToStoredPath())));

	/// <summary>
	/// <c>#paths</c> from the xml of <see cref="ToPathList"/>, with an index and its statistics: the plan of the query that follows
	/// does not depend on a guess of the number of xml nodes. The table is dropped when the command ends (sp_executesql).
	/// <list type="bullet">
	/// <item>No user-defined table type: a login needs EXECUTE on the type, which db_ddladmin, db_datareader and db_datawriter
	/// do not give when the schema dblive belongs to dbo. A temporary table and xml need no permission and work with any
	/// compatibility level (openjson needs 130).</item>
	/// <item><c>collate database_default</c>: a temporary table takes the collation of tempdb, and the comparison with
	/// <c>dblive.code.relative_path</c> would fail with a collation conflict.</item>
	/// <item>The index is not unique: paths that differ only by accent or width are different files for DbLive and equal
	/// for an accent- or width-insensitive collation.</item>
	/// <item><c>nvarchar(450)</c>: a clustered key holds at most 900 bytes, so <c>dblive.code</c> never stores a longer path
	/// (<see cref="MaxCodePathLength"/>).</item>
	/// </list>
	/// </summary>
	private const string PathsTableSql = """
		create table #paths ( value nvarchar(450) collate database_default not null );

		insert into #paths ( value )
		select x.p.value('text()[1]', 'nvarchar(450)')
		from @paths.nodes('/paths/p') as x(p);

		create clustered index ix_paths on #paths ( value );

		""";

	public async Task MarkCodeAsVerifiedAsync(IReadOnlyCollection<string> relativePaths, DateTime verifiedUtc)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(
			PathsTableSql + """
			update c
			set verified_utc = @verified_utc
			from dblive.code as c
			join #paths as p on p.value = c.relative_path;
			""",
			Parameters(new { paths = ToPathList(relativePaths), verified_utc = verifiedUtc })
		).ConfigureAwait(false);
	}

	public async Task MarkCodeAsApplyingAsync(IReadOnlyCollection<string> relativePaths)
	{
		using var cnn = GetConnection();
		_ = await cnn.ExecuteAsync(
			PathsTableSql + """
			update c
			set status = 'applying'
			from dblive.code as c
			join #paths as p on p.value = c.relative_path
			where c.status = 'applied';
			""",
			Parameters(new { paths = ToPathList(relativePaths) })
		).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<string>> MarkMissingCodeAsRemovedAsync(IReadOnlyCollection<string> projectRelativePaths, DateTime verifiedUtc)
	{
		using var cnn = GetConnection();
		return (await cnn.QueryAsync<string>(
			PathsTableSql + """
			update c
			set status = 'removed'
			  , verified_utc = @verified_utc
			output inserted.relative_path
			from dblive.code as c
			where c.status <> 'removed'
			  and not exists ( select 1 from #paths as p where p.value = c.relative_path );
			""",
			Parameters(new { paths = ToPathList(projectRelativePaths), verified_utc = verifiedUtc })
		).ConfigureAwait(false)).ToList();
	}

	public async Task<IReadOnlyCollection<CodeItemDto>> GetCodeItemsAsync()
	{
		using var cnn = GetConnection();
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
			Parameters(new
			{
				version = item.Version,
				name = item.Name,
				item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
				relative_path = item.RelativePath.ToStoredPath(),
				content_hash = item.ContentHash,
				status = item.Status.ToString().ToLowerInvariant(),
				created_utc = item.CreatedUtc
			})
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
			output inserted.version
			where version = @version
			  and item_type = @item_type
			""",
			Parameters(new
			{
				version = item.Version,
				item_type = MigrationItemTypeMapper.ToDbValue(item.ItemType),
				status = item.Status.ToString().ToLowerInvariant(),
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs
			})
		).ConfigureAwait(false);

		if (ver is null)
		{
			throw new DbLiveMigrationItemMissedSqlException($"Migration item not found. Version: {item.Version}, Type: {item.ItemType}");
		}
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
			Parameters(new
			{
				folder_type = projectFolder.ToString(),
				relative_path = relativePath.ToStoredPath(),
				started_utc = startedUtc,
				completed_utc = completedUtc,
				execution_time_ms = executionTimeMs,
				content_hash = contentHash
			})
		).ConfigureAwait(false);
	}

	/// <summary>
	/// The errors of SQL Server as it gives them. An error of a <c>GO</c> batch names the line of the file where the batch starts above
	/// them: the line of an error in the batch itself is its line in the batch, and its line in the file is <c>start + Line - 1</c>.
	/// </summary>
	private static string GetSqlServerError(SqlException? sqlEx, int? batchStartLine = null)
	{
		if (sqlEx == null)
			return "";

		var sb = new System.Text.StringBuilder();

		_ = sb.AppendLine("SQL Server error:");

		if (batchStartLine is not null)
		{
			_ = sb.AppendLine(CultureInfo.InvariantCulture, $"The batch starts at line {batchStartLine} of the file.");
		}

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
}