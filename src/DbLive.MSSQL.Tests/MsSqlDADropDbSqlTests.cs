namespace DbLive.MSSQL.Tests;

/// <summary>
/// The SQL of <c>DropDbAsync</c> by the edition of the server. Azure SQL Database rejects <c>single_user</c>, and the CI has no Azure SQL
/// Database: its branch is checked here, not run. <see cref="MsSqlDADropDbTests"/> runs the other branch.
/// </summary>
public class MsSqlDADropDbSqlTests
{
	[Fact]
	public void Azure_SQL_Database_drops_without_single_user()
	{
		Assert.Equal("drop database [db];", MsSqlDA.GetDropDbSql("[db]", MsSqlDA.AzureSqlDatabaseEngineEdition));
	}

	// 2 Standard, 3 Enterprise and Developer, 4 Express and LocalDB, 8 Azure SQL Managed Instance.
	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(8)]
	public void Other_editions_set_single_user_in_the_batch_of_the_drop(int engineEdition)
	{
		Assert.Equal(
			"alter database [db] set single_user with rollback immediate;\ndrop database [db];",
			MsSqlDA.GetDropDbSql("[db]", engineEdition).ReplaceLineEndings("\n")
		);
	}
}
