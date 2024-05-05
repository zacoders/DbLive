namespace EasyFlow.Adapter;

public class SqlResult
{
	public List<string> Columns { get; } = [];
	public List<SqlRow> Rows { get; } = [];

	public SqlResult(List<object> resultRows)
	{
		if (resultRows.Any())
		{
			var firstRows = (IDictionary<string, object>)resultRows[0];
			Columns = firstRows.Keys.ToList();
		}

		foreach (IDictionary<string, object> row in resultRows)
		{            
            SqlRow sqlRow = new(row.Values.ToList());
			Rows.Add(sqlRow);
		}
	}
}