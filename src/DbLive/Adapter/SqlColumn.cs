namespace DbLive;


public sealed record SqlColumn
{
	internal SqlColumn(string columnName, string dataType)
	{
		ColumnName = columnName;
		DataType = dataType;
	}

	public string ColumnName { get; }

	public string DataType { get; }
	public override string ToString() =>
		$"{(string.IsNullOrWhiteSpace(ColumnName) ? "noname" : ColumnName)}: {DataType}";
}