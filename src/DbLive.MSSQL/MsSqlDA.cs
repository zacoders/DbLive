using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using DbLive.Schema;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace DbLive.MSSQL;

internal class MsSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
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
	// the batches before it applied.
	public Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	) => ExecuteScriptAsync(sqlStatement, isolationLevel, timeout, ownTransaction: Transaction.Current is null);

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
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, GetSqlServerError(e), e);
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
			await ExecuteBatchesAsync(sqlConnection, batches, timeout).ConfigureAwait(false);
			_ = await sqlConnection.ExecuteAsync("commit transaction;").ConfigureAwait(false);
		}
		catch
		{
			await TryRollbackAsync(sqlConnection).ConfigureAwait(false);
			throw;
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
		catch (Exception e) when (e is not DbLiveSqlException)
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
			columnName: GetValue<string>(row["ColumnName"]),
			dataType: GetSqlTypeName(
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
			Parameters(new
			{
				relative_path = item.RelativePath.ToStoredPath(),
				item_type = item.ItemType.ToString().ToLowerInvariant(),
				status = item.Status.ToString().ToLowerInvariant(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc,
				error = FitErrorColumn(item.ErrorMessage)
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
	/// <item><c>nvarchar(450)</c>: a clustered key holds at most 900 bytes, so <c>dblive.code</c> never stores a longer path.</item>
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
				 , error as ErrorMessage
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
			  , error = @error
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
				execution_time_ms = item.ExecutionTimeMs,
				error = FitErrorColumn(item.ErrorMessage)
			})
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
			Parameters(new
			{
				relative_path = item.RelativePath.ToStoredPath(),
				content_hash = item.ContentHash,
				run_utc = item.StartedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				pass = item.IsSuccess,
				error = FitErrorColumn(item.ErrorMessage)
			})
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
	private static string GetSqlServerError(Exception ex, int? batchStartLine = null)
	{
		SqlException? sqlEx = FindSqlException(ex);

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