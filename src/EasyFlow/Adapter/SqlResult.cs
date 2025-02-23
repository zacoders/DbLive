namespace EasyFlow.Adapter;

public class SqlResult
{
	public List<SqlColumn> Columns { get; } = [];
	public List<SqlRow> Rows { get; } = [];

	public SqlResult(List<SqlColumn> sqlColumns, List<SqlRow> resultRows)
	{
		Columns = sqlColumns;
		Rows = resultRows;
	}
}
