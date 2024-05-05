
namespace EasyFlow.Adapter;

public class SqlRow
{
	public List<ColumnValue> ColumnValues { get; } = [];

	public SqlRow(IDictionary<string, object> columnValues)
	{
		foreach(var columnValue in columnValues)
		{
			ColumnValues.Add(new ColumnValue(columnValue.Key, columnValue.Value));
		}
	}
}