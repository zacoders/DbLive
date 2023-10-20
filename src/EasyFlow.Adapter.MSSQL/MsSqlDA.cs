
using System.Data;

namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDA : IEasyFlowDA
{
	static MsSqlDA()
	{
		SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		const string query = @"
			select version
				 , name
				 , created_utc
				 , execution_time_ms
			from easyflow.migrations
		";

		using var cnn = new SqlConnection(cnnString);
		return cnn.Query<MigrationDto>(query).ToList();
	}

	public bool EasyFlowInstalled(string cnnString)
	{
		const string query = @"
			select iif(object_id('easyflow.migrations', 'U') is null, 0, 1)
		";

		using var cnn = new SqlConnection(cnnString);
		return cnn.ExecuteScalar<bool>(query);
	}

	public int GetEasyFlowVersion(string cnnString)
	{
		const string query = @"
			select version
			from easyflow.version
		";

		using var cnn = new SqlConnection(cnnString);
		return cnn.ExecuteScalar<int?>(query) ?? 0;
	}

	public void SetEasyFlowVersion(string cnnString, int version, DateTime migrationDatetime)
	{
		const string query = @"
			update easyflow.version
			set version = @version
			  , modified_utc = @modified_utc;
		";

		using var cnn = new SqlConnection(cnnString);
		cnn.Query(query, new { version, modified_utc = migrationDatetime });
	}

	public void MarkMigrationAsApplied(string cnnString, int migrationVersion, string migrationName, DateTime migrationCompletedUtc, int executionTimeMs)
	{
		string query = @"
			insert into easyflow.migrations
			(
				version
			  , name
			  , created_utc
			  , execution_time_ms
			)
			values (
				@version
			  , @name
			  , @created_utc
			  , @execution_time_ms
			)
		";

		using var cnn = new SqlConnection(cnnString);
		cnn.Query(query, new
		{
			version = migrationVersion,
			name = migrationName,
			created_utc = migrationCompletedUtc,
			execution_time_ms = executionTimeMs
		});
	}

	public void ExecuteNonQuery(string cnnString, string sqlStatement)
	{
		try
		{
			using SqlConnection sqlConnection = new(cnnString);
			sqlConnection.Open();
			ServerConnection serverConnection = new(sqlConnection);
			serverConnection.ExecuteNonQuery(sqlStatement);
			serverConnection.Disconnect();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}
	}

	public void CreateDB(string cnnString, bool skipIfExists = true)
	{
		SqlConnectionStringBuilder builder = new(cnnString);
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

	public void MarkCodeAsApplied(string cnnString, string relativePath, Guid contentMD5Hash, DateTime createdUtc, int executionTimeMs)
	{
		using var cnn = new SqlConnection(cnnString);
		cnn.Query("easyflow.insert_code_state",
			new
			{
				relative_path = relativePath,
				content_md5_hash = contentMD5Hash,
				created_utc = createdUtc,
				execution_time_ms = executionTimeMs
			},
			commandType: CommandType.StoredProcedure
		);
	}

	public void MarkCodeAsVerified(string cnnString, string relativePath, DateTime verifiedUtc)
	{
		using var cnn = new SqlConnection(cnnString);
		cnn.Query(
			"easyflow.update_code_state",
			new { relative_path = relativePath, verified_utc = verifiedUtc },
			commandType: CommandType.StoredProcedure
		);
	}

	public bool IsCodeItemApplied(string cnnString, string relativePath, Guid contentMD5Hash)
	{
		using var cnn = new SqlConnection(cnnString);
		return cnn.QueryFirst<bool>(
			"easyflow.is_code_item_applied", 
			new { relative_path = relativePath, content_md5_hash = contentMD5Hash }, 
			commandType: CommandType.StoredProcedure
		);
	}

}