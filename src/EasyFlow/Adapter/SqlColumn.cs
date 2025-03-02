namespace EasyFlow.Adapter;

public record SqlColumn 
(
	string ColumnName,
	int ProviderType,
	short? NumericPrecision,
	short? NumericScale,
	int ColumnSize
);