namespace DbLive.MSSQL.Tests;

/// <summary>
/// SQL Server does not allow In-Memory OLTP DDL in a user transaction: a code file with the word <c>native_compilation</c> or
/// <c>memory_optimized</c> runs without a transaction of its own.
/// </summary>
public class MsSqlDAInMemoryOltpTests
{
	private readonly MsSqlDA _da = new(new DbLiveDbConnection(""));

	[Theory]
	[InlineData("create or alter procedure dbo.P with native_compilation, schemabinding as begin atomic with (transaction isolation level = snapshot, language = N'us_english') select 1 as x; end")]
	[InlineData("create type dbo.TT as table (id int not null primary key nonclustered) with (memory_optimized = on);")]
	[InlineData("CREATE TYPE dbo.TT AS TABLE (id int NOT NULL PRIMARY KEY NONCLUSTERED) WITH (MEMORY_OPTIMIZED=ON);")]
	[InlineData("create or alter view dbo.V as select 1 as x;\ngo\ncreate or alter procedure dbo.P\nwith Native_Compilation, schemabinding\nas begin atomic with (transaction isolation level = snapshot, language = N'us_english') select 1 as x; end")]
	// in a comment too: the file only loses its transaction.
	[InlineData("-- not native_compilation any more\ncreate or alter procedure dbo.P as select 1 as x;")]
	public void File_with_in_memory_oltp_ddl_runs_without_a_transaction(string script)
	{
		Assert.True(_da.RunsCodeItemWithoutTransaction(script));
	}

	[Theory]
	[InlineData("create or alter procedure dbo.P as select 1 as x;")]
	// columns of the catalog views and the filegroup of In-Memory OLTP are not DDL of a memory-optimized object.
	[InlineData("create or alter view dbo.V as select name from sys.tables where is_memory_optimized = 1;")]
	[InlineData("create or alter view dbo.V as select object_id from sys.sql_modules where uses_native_compilation = 1;")]
	[InlineData("select name from sys.filegroups where type = 'FX' and type_desc = 'memory_optimized_data_filegroup';")]
	[InlineData("alter database current set memory_optimized_elevate_to_snapshot = on;")]
	[InlineData("")]
	public void File_without_in_memory_oltp_ddl_runs_in_its_transaction(string script)
	{
		Assert.False(_da.RunsCodeItemWithoutTransaction(script));
	}
}
