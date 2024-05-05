namespace EasyFlow.Adapter;

public class ColumnValue(string columnName, object value)
{
	public string ColumnName { get; set; } = columnName;
	public object Value { get; set; } = value;
}