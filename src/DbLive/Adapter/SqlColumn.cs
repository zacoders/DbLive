namespace DbLive.Adapter;


public record SqlColumn(string ColumnName, string DataType)
{
	public override string ToString() =>
		$"{(string.IsNullOrWhiteSpace(ColumnName) ? "noname" : ColumnName)}: {DataType}";
}