namespace EasyFlow.Adapter;

public class SqlResult
{
	public List<SqlRow> Rows { get; } = [];

	public SqlResult(List<object> resultRows)
	{
		foreach (IDictionary<string, object> row in resultRows)
		{
			SqlRow sqlRow = new(row);
			Rows.Add(sqlRow);
		}
	}
}