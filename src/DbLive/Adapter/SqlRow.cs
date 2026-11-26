
namespace DbLive;

internal sealed class SqlRow : List<object>
{
	internal SqlRow(params object[] columnValues)
		: base(columnValues)
	{
	}
}