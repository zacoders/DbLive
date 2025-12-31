using DbLive.Adapter;
using DbLive.Common;
using DbLive.Project;
using System.Collections.Specialized;
using System.Data;

namespace DbLive.MSSQL;

public class MsSqlDA(IDbLiveDbConnection _cnn) : IDbLiveDA
{
	static MsSqlDA()
	{
		SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
		DefaultTypeMap.MatchNamesWithUnderscores = true;
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

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationItemDto>(query).ToList();
	}

	public bool DbLiveInstalled()
	{
		const string query = @"
			select iif(object_id('dblive.version', 'U') is null, 0, 1)
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public int GetCurrentMigrationVersion()
	{
		const string query = @"
			select version
			from dblive.dbversion
		";
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public int GetDbLiveVersion()
	{
		const string query = @"
			select version
			from dblive.version
		";
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public void SetDbLiveVersion(int version, DateTime migrationDateTime)
	{
		const string query = @"
			update dblive.version
			set version = @version
			  , applied_utc = @applied_utc;
		";
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new { version, applied_utc = migrationDateTime });
	}

	public void SaveCurrentMigrationVersion(int version, DateTime migrationCompletedUtc)
	{
		const string query = @"
			update dblive.dbversion
			set version = @version
			  , applied_utc = @applied_utc;
		";
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new
		{
			version,
			applied_utc = migrationCompletedUtc
		});
	}

	public void ExecuteNonQuery(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		try
		{
			using SqlConnection sqlConnection = new(_cnn.ConnectionString);
			sqlConnection.Open();
			SetTransactionIsolationLevel(sqlConnection, isolationLevel);
			ServerConnection serverConnection = new(sqlConnection)
			{
				StatementTimeout = GetTimeoutSeconds(timeout)
			};
			serverConnection.ExecuteNonQuery(sqlStatement);
			serverConnection.Disconnect();
		}
		catch (Exception e)
		{
			var sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, e);
		}
	}

	private void SetTransactionIsolationLevel(SqlConnection cnn, TranIsolationLevel isolationLevel)
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
		cnn.Execute($"SET TRANSACTION ISOLATION LEVEL {isolationLevelSql};");
	}

	private int GetTimeoutSeconds(TimeSpan? timeout)
	{
		return timeout.HasValue ? (int)timeout.Value.TotalSeconds : 30;
	}

	public List<SqlResult> ExecuteQueryMultiple(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	)
	{
		try
		{
			using SqlConnection cnn = new(_cnn.ConnectionString);

			SetTransactionIsolationLevel(cnn, isolationLevel);

			using SqlCommand cmd = cnn.CreateCommand();
			cmd.CommandTimeout = GetTimeoutSeconds(timeout);
			cmd.CommandText = sqlStatement;

			cnn.Open();

			using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo);

			List<SqlResult> multipleResults = [];
			do
			{
				SqlResult? sqlResult = ReadResult(reader);
				if (sqlResult is not null)
				{
					multipleResults.Add(sqlResult);
				}
			}
			while (reader.NextResult());


			return multipleResults;
		}
		catch (Exception e)
		{
			var sqlException = e.Get<SqlException>();
			throw new DbLiveSqlException(sqlException?.Message ?? e.Message, e);
		}
	}

	private static SqlResult? ReadResult(SqlDataReader reader)
	{
		DataTable schemaTable = reader.GetSchemaTable();

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
		while (reader.Read())
		{
			SqlRow row = new();
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
		if (objectValue == DBNull.Value) throw new Exception("Expected non null value, but NULL received.");
		return (T)objectValue;
	}

	public void CreateDB(bool skipIfExists = true)
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string databaseToCreate = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		SqlConnection cnn = new(builder.ConnectionString);
		cnn.Open();

		var cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)cmd.ExecuteScalar() == 1;

		if (dbExists && skipIfExists) return;

		ServerConnection serverCnn = new(cnn);
		serverCnn.ExecuteNonQuery($"create database [{databaseToCreate}];");

		serverCnn.Disconnect();
	}

	public void DropDB(bool skipIfNotExists = true)
	{
		SqlConnectionStringBuilder builder = new(_cnn.ConnectionString);
		string databaseToDrop = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		SqlConnection cnn = new(builder.ConnectionString);
		cnn.Open();

		var cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		cmd.Parameters.AddWithValue("name", databaseToDrop);
		bool dbExists = (int?)cmd.ExecuteScalar() == 1;

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
		serverCnn.ExecuteNonQuery(new StringCollection {
			$"alter database [{databaseToDrop}] set single_user with rollback immediate;",
			$"drop database [{databaseToDrop}];"
		});

		serverCnn.Disconnect();
	}

	public void SaveCodeItem(CodeItemDto item)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query("dblive.save_code_item",
			new
			{
				relative_path = item.RelativePath,
				status = item.Status.ToString().ToLower(),
				content_hash = item.ContentHash,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				created_utc = item.CreatedUtc,
				error_message = item.ErrorMessage
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"dblive.update_code_state",
			new { relative_path = relativePath, verified_utc = verifiedUtc },
			commandType: CommandType.StoredProcedure
		);
	}

	public CodeItemDto? FindCodeItem(string relativePath)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.QueryFirstOrDefault<CodeItemDto>(
			"""
			select relative_path
				 , content_hash
				 , applied_utc
				 , created_utc
				 , execution_time_ms
				 , verified_utc
				 , error_message
			     , status			  
			from dblive.code
			where relative_path = @relative_path
			""",
			new { relative_path = relativePath }
		);
	}

	public void SaveMigrationItemState(MigrationItemDto item)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"dblive.save_migration",
			new
			{
				version = item.Version,
				name = item.Name,
				item_type = item.ItemType.ToString().ToLower(),
				content_hash = item.ContentHash,
				content = item.Content,
				status = item.Status.ToString().ToLower(),
				created_utc = item.CreatedUtc,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void SaveUnitTestResult(UnitTestItemDto item)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"dblive.save_unit_test_result",
			new
			{
				relative_path = item.RelativePath,
				content_hash = item.Crc32Hash,
				run_utc = item.StartedUtc,
				execution_time_ms = item.ExecutionTimeMs,
				pass = item.IsSuccess,
				error = item.ErrorMessage
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"dblive.save_folder_item",
			new
			{
				folder_type = projectFolder.ToString(),
				relative_path = relativePath,
				started_utc = startedUtc,
				completed_utc = completedUtc,
				execution_time_ms = executionTimeMs
			},
			commandType: CommandType.StoredProcedure
		);
	}
}