namespace DbLive.Adapter;

public class SqlResult
{
	public List<SqlColumn> Columns { get; } = [];
	public List<SqlRow> Rows { get; } = [];

	public SqlResult(List<SqlColumn> sqlColumns, List<SqlRow> resultRows)
	{
		Columns = sqlColumns;
		Rows = resultRows;
	}

	public T? GetValue<T>(string columnName, int rowNumber)
	{
		int columnIndex = Columns.FindIndex(x => x.ColumnName == columnName);
		
		if (columnIndex < 0) return default;

		return (T)Rows[rowNumber][columnIndex];
	}
}
