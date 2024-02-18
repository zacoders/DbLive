using EasyFlow.Adapter;
using EasyFlow.Common;
using EasyFlow.Project;
using System.Collections.Specialized;
using System.Data;

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

	public void MarkCodeAsApplied(string relativePath, int contentHash, DateTime appliedUtc, int executionTimeMs)
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
				item_type = item.ItemType,
				content_hash = item.ContentHash,
				content = item.Content,
				status = item.Status,
				created_utc = item.CreatedUtc,
				applied_utc = item.AppliedUtc,
				execution_time_ms = item.ExecutionTimeMs
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void SaveUnitTestResult(string relativePath, int crc32Hash, DateTime startedUtc, int executionTimeMs, bool isSuccess, string? errorMessage)
	{
		using var cnn = new SqlConnection(_cnn.ConnectionString);
		cnn.Query(
			"easyflow.save_unit_test_result",
			new
			{
				relative_path = relativePath,
				content_hash = crc32Hash,
				run_utc = startedUtc,
				execution_time_ms = executionTimeMs,
				pass = isSuccess,
				error = errorMessage
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, int executionTimeMs)
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