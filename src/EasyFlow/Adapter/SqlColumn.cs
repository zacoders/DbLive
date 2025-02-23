namespace EasyFlow.Adapter;

public class SqlColumn
{
	public required string ColumnName { get; set; }
	public required int ProviderType { get; set; }
	public short? NumericPrecision { get; set; }
	public short? NumericScale { get; set; }
	public int ColumnSize { get; set; }
}