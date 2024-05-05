
namespace EasyFlow.Adapter;

public class SqlRow
{
	public List<object> ColumnValues { get; } = [];

	public SqlRow(List<object> columnValues)
	{
		ColumnValues = columnValues;
	}
}