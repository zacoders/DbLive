using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace DbLive.MSSQL.Tests;


/// <summary>
/// Paths that differ only by accent are different files for DbLive, and equal for an accent-insensitive collation.
/// </summary>
[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class AccentInsensitiveCollationTests(SqlServerIntegrationFixture fixture, ITestOutputHelper output)
	: SqlServerDbTests(fixture, output, createDb: false)
{
	[Fact]
	public async Task Code_files_that_differ_only_by_accent_deploy_to_an_accent_insensitive_database()
	{
		using (SqlConnection cnn = new(Fixture.MasterDbConnectionString))
		{
			await cnn.OpenAsync();
			using SqlCommand cmd = cnn.CreateCommand();
			cmd.CommandText = $"create database [{DbName}] collate Latin1_General_CI_AI;";
			_ = await cmd.ExecuteNonQueryAsync();
		}

		using TempProject project = new TempProject()
			.AddFile("Code/café.sql", "create or alter view dbo.v_accent as select 1 as one;")
			.AddFile("Code/cafe.sql", "create or alter view dbo.v_no_accent as select 1 as one;");

		DbLiveBuilder builder = CreateBuilder(project);

		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);

		// the second deployment skips a file by its hash and sets verified_utc of the files by their paths.
		await builder.CreateDeployer().DeployAsync(DeployParameters.Default);
	}
}
