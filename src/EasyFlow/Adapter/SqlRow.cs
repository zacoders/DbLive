
namespace EasyFlow.Adapter;

public class SqlRow : List<object> 
{
	public SqlRow(params object[] columnValues)
		: base(columnValues)
	{
	}
}