using EasyFlow.Adapter;
using EasyFlow.Common;
using EasyFlow.Project;
using System.Collections.Specialized;
using System.Data;
using System.Data.SqlTypes;

namespace EasyFlow.MSSQL;

public class MsSqlDA(IEasyFlowDbConnection _cnn) : IEasyFlowDA
{
	static MsSqlDA()
	{
		SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations()
	{
		const string query = @"
			select version
				 , name
				 , created_utc
				 , modified_utc
			from easyflow.migration
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationDto>(query).ToList();
	}

	public IReadOnlyCollection<MigrationItemDto> GetNonAppliedBreakingMigrationItems()
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
			from easyflow.migration_item
			where status != 'applied'
			  and item_type = 'breakingchange'
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.Query<MigrationItemDto>(query).ToList();
	}

	public bool EasyFlowInstalled()
	{
		const string query = @"
			select iif(object_id('easyflow.migration', 'U') is null, 0, 1)
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public int GetEasyFlowVersion()
	{
		const string query = @"
			select version
			from easyflow.version
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public void SetEasyFlowVersion(int version, DateTime migrationDateTime)
	{
		const string query = @"
			update easyflow.version
			set version = @version
			  , applied_utc = @applied_utc;
		";

		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(query, new { version, applied_utc = migrationDateTime });
	}

	public void SaveMigration(int migrationVersion, string migrationName, DateTime migrationModificationUtc)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"easyflow.save_migration",
			new
			{
				version = migrationVersion,
				name = migrationName,
				created_utc = migrationModificationUtc,
				modified_utc = migrationModificationUtc
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void ExecuteNonQuery(string sqlStatement)
	{
		try
		{
			using SqlConnection sqlConnection = new(_cnn.ConnectionString);
			sqlConnection.Open();
			ServerConnection serverConnection = new(sqlConnection);
			serverConnection.ExecuteNonQuery(sqlStatement);
			serverConnection.Disconnect();
		}
		catch (Exception e)
		{
			var sqlException = e.Get<SqlException>();
			throw new EasyFlowSqlException(sqlException?.Message ?? e.Message, e);
		}
	}

	public MultipleResults ExecuteQueryMultiple(string sqlStatement)
	{
		try
		{
			using SqlConnection cnn = new(_cnn.ConnectionString);

			using SqlCommand cmd = cnn.CreateCommand();
			cmd.CommandText = sqlStatement;

			cnn.Open();

			using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo);

			MultipleResults multipleResults = [];
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
			throw new EasyFlowSqlException(sqlException?.Message ?? e.Message, e);
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

	public void MarkCodeAsApplied(string relativePath, int contentHash, DateTime appliedUtc, long executionTimeMs)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query("easyflow.insert_code_state",
			new
			{
				relative_path = relativePath,
				content_hash = contentHash,
				applied_utc = appliedUtc,
				execution_time_ms = executionTimeMs
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"easyflow.update_code_state",
			new { relative_path = relativePath, verified_utc = verifiedUtc },
			commandType: CommandType.StoredProcedure
		);
	}

	public CodeItemDto? FindCodeItem(string relativePath)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		return cnn.QueryFirstOrDefault<CodeItemDto>(
			"easyflow.get_code_item",
			new { relative_path = relativePath },
			commandType: CommandType.StoredProcedure
		);
	}

	public void SaveMigrationItemState(MigrationItemDto item)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"easyflow.save_migration_item",
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
			"easyflow.save_unit_test_result",
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
			"easyflow.save_folder_item",
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